module Kinetic.Network

open System.IO
open ProtoBuf
open System.Net.Sockets
open System.Security.Cryptography
open Kinetic.Proto

type KineticClient = TcpClient

let SECRET = System.Text.Encoding.UTF8.GetBytes("asdfasdf") // its for dev only

let byteToHex bytes = 
    bytes 
    |> Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x))
    |> String.concat " "

let bigEndian (value : int) =
    let a = System.BitConverter.GetBytes(value)
    System.Array.Reverse a
    a

let calculateHmac secret data offset length = 
    use hmac = new HMACSHA1(secret)
    let hash =  Array.zeroCreate 20
    hmac.Initialize()
    hmac.TransformBlock(bigEndian length, 0, 4, hash, 0) |> ignore
    hmac.TransformFinalBlock(data, offset, length) |> ignore
    hmac.Hash

let rec asyncSafeRead (stream : Stream) buffer offset length =
    async {
        // printfn "Reading %A bytes from position %A" length offset
        let! read = stream.AsyncRead(buffer, offset, length)
        if read = 0 then failwith "End of stream reached."
        if read < length then
            do! asyncSafeRead stream buffer (offset + read) (length - read)
    }

let AsyncReceive (client : KineticClient) : Async<Message * bytes option> =
    async {
        let ns = client.GetStream()
        let buffer = Array.zeroCreate 9
        do! asyncSafeRead ns buffer 0 9
        if buffer.[0] <> 70uy then failwith "Invalid magic number received!"
        System.Array.Reverse buffer
        let valueln = System.BitConverter.ToInt32(buffer, 0)
        let protoln = System.BitConverter.ToInt32(buffer, 4)
        // printfn "Magic=%A, Proto=%A bytes, Value=%A bytes" buffer.[8] protoln valueln // index 8, its reversed
        let buffer = Array.zeroCreate protoln
        do! asyncSafeRead ns buffer 0 protoln
        use ms = new MemoryStream(buffer)
        let resp = Serializer.Deserialize(ms)
        if valueln > 0 then 
            let buffer = Array.zeroCreate valueln
            do! asyncSafeRead ns buffer 0 valueln
            return (resp, Some buffer)
        else return (resp, None)
    }

type Stream with

    member x.AsyncRead  buffer offset count  = 
        Async.FromBeginEnd(buffer, offset, count,
                            (fun (buffer, offset, count, callback, state) -> x.BeginRead(buffer, offset, count, callback, state)),
                            x.EndRead)

type Socket with
    member x.AsyncSend(buffer, offset, count) =
        Async.FromBeginEnd(buffer, offset, count,
                            (fun (buffer, offset, count, callback, state) -> x.BeginSend(buffer, offset, count, SocketFlags.None, callback, state)),
                            x.EndSend)

    member x.AsyncSendChunks(buffer, offset, count, chunk_size) =
        async {
            let sent = ref 0
            while !sent < count do
                if count - !sent > chunk_size then
                    let! x = x.AsyncSend(buffer, offset + !sent, chunk_size)
                    sent := !sent + x
                else
                    let! x = x.AsyncSend(buffer, offset + !sent, count - !sent)
                    sent := !sent + x
        }
                   
    member x.SendChunks(buffer, offset, count, chunk_size) =
        let sent = ref 0
        while !sent < count do
            if count - !sent > chunk_size then
                let x = x.Send(buffer, offset + !sent, chunk_size, SocketFlags.None)
                sent := !sent + x
            else
                let x = x.Send(buffer, offset + !sent, count - !sent, SocketFlags.None)
                sent := !sent + x

type MemoryStream with
    member x.AsyncCopyTo (stream : Stream) = 
        async { 
            do! stream.AsyncWrite(x.GetBuffer(), int x.Position, int x.Length)
        }           
 


let AsyncSendSocket (proto : Message) (value : bytes) (s : Socket) =
    async {
        
        if proto.Hmac = null then
            // calculate hmac
            use ms = new MemoryStream()
            Serializer.Serialize(ms, proto.Command)
            proto.Hmac <- calculateHmac SECRET (ms.GetBuffer()) 0 (int ms.Length)

        use ms = new MemoryStream()
        ms.Seek(9L, SeekOrigin.Begin) |> ignore
        Serializer.Serialize(ms, proto)

        // write header on stream
        ms.Seek(0L, SeekOrigin.Begin) |> ignore
        ms.WriteByte(70uy) // Magic Number = 70 (byte) 
        let ln = bigEndian <| int (ms.Length - 9L)
        ms.Write(ln, 0, 4) // Proto length (int32)
        match value with // Value length (int32)
        | null -> () // nothing to encode, null is 0 bytes that encodes to 00 00 00 00
        | buffer ->  let ln = bigEndian (buffer.Length)
                     ms.Write(ln, 0, 4) 
        ms.Seek(0L, SeekOrigin.Begin) |> ignore // move to start
                     
        do! s.AsyncSend(ms.GetBuffer(), int ms.Position, int ms.Length) |> Async.Ignore

        match value with
        | null -> ()
        | buffer -> do! s.AsyncSend(buffer, 0, buffer.Length) |> Async.Ignore // Send buffered value
    }  

let AsyncSend proto value (client : KineticClient) =
    AsyncSendSocket proto value client.Client
