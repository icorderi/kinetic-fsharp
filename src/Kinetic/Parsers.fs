module Kinetic.Model.Parsers

open Kinetic.Proto
open Kinetic.Model

let noneParser _ = Response.None 

type internal Input = Kinetic.Proto.Message * Kinetic.Proto.Command * bytes option
type Parser = Input -> Response 

let parseKeyValue ((m, r, v) : Input) =  
    Response.KeyValue { Key = r.Body.KeyValue.Key
                        Value = v.Value
                        Metadata = { Tag = r.Body.KeyValue.Tag
                                     Algorithm = r.Body.KeyValue.Algorithm
                                     Version = r.Body.KeyValue.DbVersion
                        }}

type Command with
    
    member x.Parser () : Parser =
        match x with
        | Noop -> noneParser
        | Put _ -> noneParser
        | Get _ -> parseKeyValue
        | GetKeyRange _ -> fun (_,r,_) -> Response.KeysRange <| Seq.toList r.Body.Range.Keys
        | Delete c -> noneParser
        | Erase -> noneParser
        | SecureErase -> noneParser
        | Lock -> noneParser
        | Unlock -> noneParser
        | WithParams (inner,_) -> inner.Parser()       
        | _ -> fun (_,r,v) -> Response.Raw (r,v)
