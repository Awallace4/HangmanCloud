namespace FsAzureHelper

open Microsoft.WindowsAzure
open Microsoft.WindowsAzure.StorageClient
open Microsoft.WindowsAzure.ServiceRuntime

// Contains types and functions that support the use of Windows Azure,
// including Async methods for queue operations.
module FsAzureHelpers =

    // Extension methods to support async cloud queues
    type CloudQueue with
        member this.AddMessageAsync(message) =
            Async.FromBeginEnd(message, (fun (message, callback, state) ->
                                this.BeginAddMessage  (message, callback, state)),
                                this.EndAddMessage)
        member this.ClearAsync() =
            Async.FromBeginEnd((fun (callback, state) -> this.BeginClear(callback, state)),
                                this.EndClear)
        member this.CreateAsync() =
            Async.FromBeginEnd((fun (callback, state) -> this.BeginCreate(callback, state)),
                                this.EndCreate)
        member this.CreateIfNotExistAsync() =
            Async.FromBeginEnd((fun (callback, state) -> this.BeginCreateIfNotExist(callback, state)),
                               this.EndCreateIfNotExist)
        member this.DeleteAsync() =
            Async.FromBeginEnd((fun (callback, state) -> this.BeginDelete(callback, state)),
                               this.EndDelete)
        member this.DeleteMessageAsync(message) =
            Async.FromBeginEnd(message, (fun (message, callback, state) ->
                                this.BeginDeleteMessage(message, callback, state)),
                                this.EndDeleteMessage)
        member this.DeleteMessageAsync(messageId, popReceipt) =
            Async.FromBeginEnd(messageId, popReceipt, (fun (messageId, popReceipt, callback, state) ->
                               this.BeginDeleteMessage(messageId, popReceipt, callback, state)),
                               this.EndDeleteMessage)
        member this.ExistsAsync() =
            Async.FromBeginEnd((fun (callback, state) -> this.BeginExists(callback, state)),
                               this.EndDelete)
        member this.FetchAttributesAsync() =
            Async.FromBeginEnd((fun (callback, state) -> this.BeginFetchAttributes(callback, state)),
                               this.EndFetchAttributes)
        member this.GetMessageAsync() =
            Async.FromBeginEnd((fun (callback, state) -> this.BeginGetMessage(callback, state)),
                                this.EndGetMessage)
        member this.GetMessagesAsync(count) =
            Async.FromBeginEnd(count, (fun (count, callback, state) -> this.BeginGetMessages(count, callback, state)),
                        this.EndGetMessages)
        member this.PeekMessageAsync() =
            Async.FromBeginEnd((fun (callback, state) -> this.BeginPeekMessage(callback, state)),
                                this.EndPeekMessage)
        member this.PeekMessagesAsync(count) =
            Async.FromBeginEnd(count, (fun (count, callback, state) -> this.BeginPeekMessages(count, callback, state)),
                        this.EndPeekMessages)
        member this.SetMetadataAsync() =
            Async.FromBeginEnd((fun (callback, state) -> this.BeginSetMetadata(callback, state)),
                        this.EndSetMetadata)
        member this.UpdateMessageAsync(message, visibilityTimeout, updateFields) =
            Async.FromBeginEnd(message, visibilityTimeout, updateFields, 
                               (fun (message, visibilityTimeout, updateFields, callback, state) ->
                                   this.BeginUpdateMessage(message, visibilityTimeout, updateFields, callback, state)),
                               this.EndUpdateMessage)