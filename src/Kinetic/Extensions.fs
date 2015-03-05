module Kinetic.Extensions

open Kinetic.Model

exception InvalidResponseType 
exception OperationCancelled

type Kinetic.Client with
    
    member this.AsyncGet (x : Get) = 
        async {
            let! r = this.AsyncSendAndReceive <| Command.Get x
            match r with
            | Success (KeyValue kv) -> return kv
            | Error exn -> return raise exn
            | Cancelled -> return raise OperationCancelled
            | Success _ -> return raise InvalidResponseType "Expected KeyValue"
        }

    member this.Get x = Async.RunSynchronously <| this.AsyncGet x

    member this.AsyncPut (x : Put) = 
        async {
            let! r = this.AsyncSendAndReceive <| Command.Put x
            match r with
            | Success None -> return ()
            | Error exn -> return raise exn
            | Cancelled -> return raise OperationCancelled
            | Success _ -> return raise InvalidResponseType "Expected None"
        }
            
    member this.Put (x : Put) = Async.RunSynchronously <| this.AsyncPut x