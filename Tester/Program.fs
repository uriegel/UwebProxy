open UwebProxy

let configuration = Configuration.create {
    Configuration.createEmpty() with 
        Port = 9865
        Requests = [ useReverseProxyByHost "127.0.0.40" "http://fritz.box"]
}

let server = Server.create configuration 
server.start ()
stdin.ReadLine() |> ignore
server.stop ()