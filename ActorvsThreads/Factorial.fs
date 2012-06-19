//module Factorial
open System.Diagnostics 

// Some Helpers to make it more look like Erlang
let time asy = 
    let watch = Stopwatch.StartNew()
    asy |> Async.RunSynchronously
    watch.Stop()
    printfn "Elapsed Time: %i ms" watch.ElapsedMilliseconds
    watch.Reset()

let inline (<==) (receiver:MailboxProcessor<_>) message = 
    receiver.Post(message)

let inline (==>) message (receiver:MailboxProcessor<_>) = 
    receiver.Post(message)

let inline changeProcessor f (proc1:MailboxProcessor<_>) = 
    MailboxProcessor.Start(fun self -> async {
            while true do
                let! msg = self.Receive()
                proc1 <== (f msg)
        })
            
// Adds all given values with the given values via tree to one value
//    15
//    /\
//  4    11
// / \   / \
// 2  2  4  7
// If AddFun is the simple addition 
// The Operator (AddFun) has to be commutative and associative as the 
// operations will executed as parallel as possible
// N Has to be the number of values we get
let inline createTreeProcessor processor n reply = 
    MailboxProcessor.Start(fun self ->
        let rec loop n addFun reply state = async {
            let! msg = self.Receive()
            match n, state with
            | 1, None -> 
                reply <== msg
            | _, None -> 
                return! loop n addFun reply (Some msg)
            | _, Some t ->
                async {
                    self <== (addFun t msg)
                } |> Async.Start
                return! loop (n-1) addFun reply None
        }
        loop n processor reply None
    )

// For Usage in FSI.exe
let printer = MailboxProcessor.Start(fun self ->
                async {
                    while true do
                        let! msg = self.Receive()
                        printfn "Printer got: %s" msg
                })

// This will calculate asynchronously and parallel the factorial of N and send the reply to Reply
// in fsi you can call it via "fac 12 (changeProcessor (fun x -> x.ToString()) printer)"
let fac n reply = 
    let adder = createTreeProcessor (fun x y -> x * y) n reply
    for i in n .. -1 .. 1  do adder <== bigint i

//let N = 1000
//let intelligentFac n reply = 
//    if (n <= N) then fac n reply
//    let lastItems = createAdder (fun x y -> x * y) (N) reply
//    let adder = createAdder (fun x y -> x * y) (n-N) lastItems
//    for i in n .. -1 .. (N+1) do adder <== bigint i
//    for i in 2 .. N do lastItems <== bigint i


let naiveFac n = 
    let rec addNaive list f =
        match list with
        | first :: secound :: tail -> addNaive ((f first secound)::tail) f
        | item :: [] -> item
        | [] -> failwith "should not happen"
    addNaive ([1..n] |> List.map (fun i -> bigint i)) (fun a b -> a * b)


let betterFac n =
    match n with
    | _ when n < 0 -> failwith "invalid n"
    | _ when n < 2 -> 1I
    | _ ->
        let currentN = ref 1;
        let rec product n = 
            let m = n /2
            if (m = 0) then 
                currentN := !currentN + 2
                bigint (!currentN)
            else
                if (n = 2) then
                    let old = !currentN
                    currentN := !currentN + 4
                    (bigint old + 2I) * (bigint old + 4I)
                else
                    product(n-m) * product(m)

        let rec helperFunc p r h shift high log2n =
            if (h = n) then r
            else
                let shift = shift + h
                let h = n >>> log2n
                let log2n = log2n - 1
                let len = high
                let high = (h - 1) ||| 1
                let len = (high - len) / 2
                let p, r = 
                    if (len > 0) 
                    then 
                        let p = p * product(len)
                        p, r * p
                    else p, r
                helperFunc p r h shift high log2n

        helperFunc 1I 1I 0 0 1 (int (System.Math.Floor(System.Math.Log(float n, 2.0)))) 
        


// This will execute fac(N, Reply) and post the execution time (aynchronously)
let timeFac n = 
    async {
        let receiver = MailboxProcessor.Start(fun inbox -> async{()})
        fac n receiver
        let! msg = receiver.Receive()
        printfn "Finished fac for %i" n
    } |> time
    
let timeNaiveFac n = 
    async {
        naiveFac n |> ignore
        printfn "Finished naivefac for %i" n
    } |> time

let timeBetterFac n = 
    async {
        betterFac n |> ignore
        printfn "Finished naivefac for %i" n
    } |> time