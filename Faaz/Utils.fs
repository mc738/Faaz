namespace Faaz

open System.IO
open System.Security.Cryptography
open System.Text

module Utils =
    
    [<RequireQualifiedAccess>]
    module Conversions =
        
        let fromUtf8 (str: string) = str |> Encoding.UTF8.GetBytes
        
        let toUtf8 (bytes: byte array) = bytes |> Encoding.UTF8.GetString
        
        let bytesToHex (bytes: byte array) =
            bytes
            |> Array.fold (fun (sb: StringBuilder) b -> sb.AppendFormat("{0:x2}", b)) (StringBuilder(bytes.Length * 2))
            |> fun sb -> sb.ToString()
        
    [<RequireQualifiedAccess>]
    module Hashing =

        let generateHash (hasher: SHA256) (bytes: byte array) = hasher.ComputeHash bytes |> Conversions.bytesToHex

        /// Hash a stream and reset it to the start.
        let hashStream (hasher: SHA256) (stream: Stream) =
            stream.Seek(0L, SeekOrigin.Begin) |> ignore
            let hash = hasher.ComputeHash stream |> Conversions.bytesToHex
            stream.Seek(0L, SeekOrigin.Begin) |> ignore
            hash
            
    [<RequireQualifiedAccess>]
    module Encryption =

        module private Internal =
            open System
            
            let encryptBytesAes key salt (data: byte array) =
                use aes = Aes.Create()

                aes.Padding <- PaddingMode.PKCS7

                let encryptor = aes.CreateEncryptor(key, salt)

                let ms = new MemoryStream()

                use cs =
                    new CryptoStream(ms, encryptor, CryptoStreamMode.Write)

                cs.Write(ReadOnlySpan(data))
                cs.FlushFinalBlock()

                ms.ToArray()

            let decryptBytesAes key salt (cipher: byte array) =
                use aes = Aes.Create()

                aes.Padding <- PaddingMode.PKCS7

                let decryptor = aes.CreateDecryptor(key, salt)

                use ms = new MemoryStream(cipher)

                use cs =
                    new CryptoStream(ms, decryptor, CryptoStreamMode.Read)

                use os = new MemoryStream()
                cs.CopyTo(os)
                os.ToArray()

            let generateSalt length =

                let bytes: byte array = Array.zeroCreate length

                use rng = new RNGCryptoServiceProvider()

                rng.GetBytes(bytes)

                bytes

        /// Encrypt data with a key. A salt will be generated and appended to the front of the result
        let encrypt key data =
            let salt = Internal.generateSalt 16

            Array.concat [ salt
                           Internal.encryptBytesAes key salt data ]

        /// Takes encrypted data with a 16 byte salt appended to the from and decrypts it.
        let decrypt key (data: byte array) =
            match data.Length > 16 with
            | true ->
                Ok(
                    data
                    |> Array.splitAt 16
                    |> (fun (salt, d) -> Internal.decryptBytesAes key salt d)
                )
            | false -> Error "Input is to short to contain a valid salt."
            
    module Compression =
        open System.IO.Compression
        
        let zip (path: string) (output: string) =
            ZipFile.CreateFromDirectory(path, output, CompressionLevel.Optimal, false)