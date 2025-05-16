
let run (name: string option) =
    match name with
    | Some v -> printfn $"Hello, {v}!"
    | None -> printfn "Hello, World!"
    
let run1 (name: string) = printfn $"Hello, {name}!"