let configuration = Configuration.create {
    Configuration.createEmpty() with 
        Port = 9865
        Requests = [ ]
}

let server = Server.create configuration 
server.start ()
stdin.ReadLine() |> ignore
server.stop ()