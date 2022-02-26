namespace Faaz

open Fipc.Core.Common

/// <summary>Functions, types, helpers etc. to be used within scripts.</summary>
module ScriptingContext =
    
    open Messaging
    
    type Context = {
        OutputWriter: FipcConnectionWriter
    }
    
    let connect id = createClient "script" id   
    
    let log ctx message =
        ()