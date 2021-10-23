namespace Faaz

[<RequireQualifiedAccess>]
module IO =
     
     let read (path: string) =
        match System.IO.File.Exists path with
        | true -> attempt (fun _ -> System.IO.File.ReadAllText path)
        | false -> Error $"File `{path}` does not exist."
      
       
    //let write (path: string) (data: string) =
    //    attempt (fun _ -> System.IO.File.WriteAllText(path, data))
    

