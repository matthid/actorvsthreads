module SheetExamples

module LockExample =
    let private lockObject = new obj()
    let lockingFunction p = 
        lock lockObject (fun () ->
                printf "Locked statement %s" (p.ToString())
            )