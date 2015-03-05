namespace Kinetic

open System
open System.IO

module Log =

    type Level =
    | Any = 0
    | Debug = 10
    | Info  = 50
    | Warn  = 100
    | Error = 150
    | Fatal = 200
    | None = 255

    type private LogCommand =
    | Log of Level * string * System.DateTime 
    | Flush
    | Close of AsyncReplyChannel<unit>


    type Logger(name) as this  =

        let levelToString level =
            match level with
            | Level.None  -> "NONE"
            | Level.Debug -> "DEBUG" 
            | Level.Info  -> "INFO"
            | Level.Warn  -> "WARN"
            | Level.Error -> "ERROR"
            | Level.Fatal -> "FATAL"
            | Level.Any -> "ANY"
            | undef -> undef.ToString()
       
        let agent = MailboxProcessor.Start (fun agent ->
            // Do the loop until the Stop command is received
            // Keep the number of lines written to the log
            let rec loop(count) = async {
                let! command = agent.Receive()
                match command with
                | Log (level,message,t) -> 
                    let count = count + 1
                    let str = sprintf "%s %s [%s]: %s" (t.ToString("yyyy-MM-dd hh:mm:ss.ffff")) name (levelToString level) message
                    printfn "%s" str
                    return! loop(count)
                | Flush ->
                    return! loop(count)
                | Close reply ->
                    if this.isEnabled Level.Debug then
                        let message = sprintf "%d messages written into log" count
                        Console.WriteLine message
                    this.doClose()
                    reply.Reply(ignore())
                    return ignore()
            }

            loop(0))

        interface IDisposable with
            member this.Dispose() = this.doClose()
        
        member private this.doClose() = 
            if this.isEnabled Level.Debug then
                let message = sprintf "Discarding %d messages in the queue" (agent.CurrentQueueLength)
                Console.WriteLine(message)

            let d = agent :> IDisposable
            d.Dispose()

        member private this.log level message = 
            if level >= this.Level then
                (level, message, System.DateTime.Now) |> LogCommand.Log |> agent.Post
        
        member this.fatal fmt = Printf.ksprintf (this.log Level.Fatal) fmt
        member this.error fmt = Printf.ksprintf (this.log Level.Error) fmt
        member this.warn  fmt = Printf.ksprintf (this.log Level.Warn)  fmt
        member this.info  fmt = Printf.ksprintf (this.log Level.Info)  fmt
        member this.debug fmt = Printf.ksprintf (this.log Level.Debug) fmt

        member this.queueLength = agent.CurrentQueueLength
        member this.flush() = LogCommand.Flush |> agent.Post
        member this.close() = LogCommand.Close |> agent.PostAndReply

        member val Level = Level.Warn with get,set
        member this.isEnabled level = level >= this.Level


    let defaultLogger = new Logger("<default>")

    let isEnabled = defaultLogger.isEnabled
        
    let fatal = defaultLogger.fatal
    let error = defaultLogger.error
    let warn  = defaultLogger.warn
    let info  = defaultLogger.info
    let debug = defaultLogger.debug

    let flush = defaultLogger.flush
    let close = defaultLogger.close

