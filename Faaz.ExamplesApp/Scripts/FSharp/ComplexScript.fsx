#load "HelloWorld.fsx"
#r ""

let run (name: string option) =
    printfn "Complex script..."
    HelloWorld.run name
    