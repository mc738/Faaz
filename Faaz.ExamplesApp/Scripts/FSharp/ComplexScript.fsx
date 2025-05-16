#load "HelloWorld.fsx"
#r ""

let run (name: string option) =
    HelloWorld.run name
    