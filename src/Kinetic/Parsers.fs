// Copyright (c) 2015 Ignacio Corderi

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

// author: Ignacio Corderi

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
