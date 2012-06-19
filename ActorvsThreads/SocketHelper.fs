
module SocketHelper

open System.Collections.Generic
open System.Net
open System.Net.Sockets

let toIList<'T> (data : 'T array) =
    let segment = new System.ArraySegment<'T>(data)
    let data = new List<System.ArraySegment<'T>>() :> IList<System.ArraySegment<'T>>
    data.Add(segment)
    data

type Socket with
    member this.MyAcceptAsync() =
        Async.FromBeginEnd(this.BeginAccept, this.EndAccept)

    member this.MyConnectAsync(ipAddress : IPAddress, port : int) =
        Async.FromBeginEnd(ipAddress, port, (fun (a1,a2,a3,a4) -> this.BeginConnect((a1:IPAddress),a2,a3,a4)),this.EndConnect)

    member this.MySendAsync(data : byte array, flags : SocketFlags) =
        Async.FromBeginEnd(toIList data, flags, (fun (a1,a2,a3,a4) ->  this.BeginSend(a1,a2,a3,a4)), this.EndSend)

    member this.MyReceiveAsync(data : byte array, flags : SocketFlags) =
        Async.FromBeginEnd(toIList data, flags, (fun (a1,a2,a3,a4) -> this.BeginReceive(a1,a2,a3,a4)), this.EndReceive)

    member this.MyDisconnectAsync(reuseSocket) =
        Async.FromBeginEnd(reuseSocket, this.BeginDisconnect, this.EndDisconnect)