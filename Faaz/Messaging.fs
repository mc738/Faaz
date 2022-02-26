namespace Faaz

open System.Text
open System.Text.Json.Serialization
open Fipc.Core
open Fipc.Core.Common
open Fipc.Messaging

module Messaging =

    open Fipc.Messaging.Infrastructure
    
    [<CLIMutable>]
    type Message =
        { [<JsonPropertyName("from")>]
          From: string
          [<JsonPropertyName("message")>]
          Message: string }

    let createMessageBus _ = MessageBus.Default()

    let createConfiguration id pipeName =
        ({ Id = id
           ChannelType = FipcChannelType.NamedPipe pipeName
           MaxThreads = 1
           ContentType = FipcContentType.Text
           EncryptionType = FipcEncryptionType.None
           CompressionType = FipcCompressionType.None
           Key = Encoding.UTF8.GetBytes "Hello, World!" }: FipcConnectionConfiguration)

    let createClient id pipeName =
        createConfiguration id pipeName
        |> Client.startHookClient 

    let createServer id pipeName =
        createConfiguration id pipeName
        |> Server.startHookServer
        
    //let startListener (handlerFn: listener)