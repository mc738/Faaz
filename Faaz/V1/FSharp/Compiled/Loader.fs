namespace Faaz.V1.FSharp.Compiled

open System.Reflection

module Loader =
    
    let loadAssembly (path: string) =
        try
            Assembly.LoadFile path |> Ok
        with
        | ex -> Error ex.Message

