module SheetExamples

module LockExample =
    let private lockObject = new obj()
    let lockingFunction p = 
        lock lockObject (fun () ->
                printf "Locked statement %s" (p.ToString())
            )

module ActorExample = 
    
    type CounterMessage = 
        | Stop
        | Go
        | Start of AsyncReplyChannel<int>
    let counter = MailboxProcessor.Start(fun inbox -> 
        let rec loop count rep =
            async {
                let! msg = inbox.Receive()
                match msg with 
                | Start (replyChannel) -> return! loop 0 (Some replyChannel)
                | Go -> return! loop (count+1) rep
                | Stop -> return ()
            }
        loop 0 None)

    let testCounter() =
        async {
            let! finished = counter.PostAndAsyncReply(fun rep -> Start(rep));
            printf "Counter got to %i" finished
        } |> Async.RunSynchronously
            
            