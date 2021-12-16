namespace Faaz.ToolKit.Data

open System

module Artifact =

    open System.IO
    open System.Security.Cryptography
    open System.Text
    open Freql.Core.Common.Types
    open Freql.Sqlite
    open Faaz.Utils

    module Internal =

        let filesTable =
            """
                CREATE TABLE files (
	                name TEXT NOT NULL,
	                "path" TEXT NOT NULL,
	                extension TEXT NOT NULL,
	                "size" INTEGER NOT NULL,
	                hash INTEGER NOT NULL,
	                file_blob BLOB NOT NULL
                );
                """

        let directoriesTable =
            """
                CREATE TABLE directories (
	                name TEXT NOT NULL,
	                "path" TEXT NOT NULL,
	                hash TEXT NOT NULL
                );
                """

        let keyValuesTable =
            """
                CREATE TABLE key_values (
	                "key" TEXT NOT NULL,
	                value TEXT NOT NULL
                );
                """

        let statsTable =
            """
                CREATE TABLE stats (
	                hash TEXT NOT NULL,
                    created_on TEXT NOT NULL
                );
                """
        
        let initialize (qh: QueryHandler) =
            [ filesTable
              directoriesTable
              keyValuesTable
              statsTable ]
            |> List.map qh.ExecuteSqlNonQuery
            |> ignore
            
        type StatsRecord = {
            Hash: string
            CreatedOn: DateTime
        }
    
    module Archiving =   
     
        open Internal
        
        type AddFileParameters =
            { Name: string
              Path: string
              Extension: string
              Size: int64
              Hash: string
              FileBlob: BlobField }

        // Path.GetRelativePath(context.BasePath, file.FullName)
        let addFile (qh: QueryHandler) (basePath: string) (file: FileInfo) (data: Stream) hash =
            qh.Insert(
                "files",
                { Name = file.Name
                  Path = Path.GetRelativePath(basePath, file.FullName)
                  Extension = Path.GetExtension(file.FullName)
                  Size = data.Length
                  Hash = hash
                  FileBlob = BlobField.FromStream data }
            )

        let processFile qh hasher basePath (file: FileInfo) =
            // Load and hash file
            printf "\tProcessing file '%s': " file.Name
            let bytes = File.OpenRead file.FullName

            let hash = Hashing.hashStream hasher bytes

            // Insert file into db.
            addFile qh basePath file bytes hash
            printfn "\u2705  "
            hash

        type AddDirectoryParameters =
            { Name: string
              Path: string
              Hash: string }

        let rec processDirectory qh hasher basePath (directory: DirectoryInfo) =

            printfn "Processing directory '%s' (%s)" directory.Name (Path.GetRelativePath(basePath, directory.FullName))

            let fileHashes =
                directory.GetFiles()
                |> List.ofSeq
                |> List.sortBy (fun fi -> fi.Name)
                |> List.map (processFile qh hasher basePath)

            let directoryHashes =
                directory.GetDirectories()
                |> List.ofSeq
                |> List.sortBy (fun di -> di.Name)
                |> List.map (processDirectory qh hasher basePath)


            let hash =
                List.concat [ fileHashes
                              directoryHashes ]
                |> List.fold (fun (sb: StringBuilder) s -> sb.Append(s)) (StringBuilder())
                |> (fun sb -> sb.ToString())
                |> Encoding.UTF8.GetBytes
                |> (Hashing.generateHash hasher)

            qh.Insert(
                "directories",
                { Name = directory.Name
                  Path = Path.GetRelativePath(basePath, directory.FullName)
                  Hash = hash }
            )

            hash

    module Extraction =
        let createDirectories (qh: QueryHandler) (outputPath: string) =
            qh.Select<Archiving.AddDirectoryParameters>("directories")
            |> List.map (fun f ->
                let path =
                    Path.Combine(outputPath, f.Path)
                Directory.CreateDirectory(path)
                )
            |> ignore

        let createFiles (qh: QueryHandler) (outputPath: string) =
            //let files =
            qh.Select<Archiving.AddFileParameters>("files")
            |> List.map (fun f ->
                let path =
                    Path.Combine(outputPath, f.Path)

                use stream = f.FileBlob.Value
                use fileStream = File.Create(path)
                stream.CopyTo(fileStream)
                )
            |> ignore

        let extractArchive (qh: QueryHandler) outputPath =
            printfn "Extracting to '%s'" outputPath
            Directory.CreateDirectory(outputPath) |> ignore
            createDirectories qh outputPath
            createFiles qh outputPath

    let create path outputPath name =    
        match Directory.Exists path with
        | true ->
            let fullPath = Path.Combine(outputPath, name)
            let qh = QueryHandler.Create(fullPath)
            Internal.initialize qh
            let hasher = SHA256.Create()
            let hash = Archiving.processDirectory qh hasher path (DirectoryInfo(path))
            qh.Insert("stats", ({ Hash = hash; CreatedOn = DateTime.UtcNow }: Internal.StatsRecord))
            qh.Close()
            fullPath |> Ok
        | false -> Error $"Directory `{path}` not found."
            
        //member a.ExtractTo(path: string) = Extraction.extractArchive a.Handler path