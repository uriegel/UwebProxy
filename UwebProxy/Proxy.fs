module UwebProxy

open System
open Session
open System.Net
open ActivePatterns

let private proxyRequest (url: string) (requestSession: RequestSession) =
    async {
        let request = requestSession.Query.Value
        let webRequest = WebRequest.Create url :?> HttpWebRequest
        webRequest.Method <- string requestSession.Method |> String.toUpperInvariant
        for h in requestSession.Header.rawHeaders do
            match h.Key |> String.toLowerInvariant with
            | "accept" -> webRequest.Accept <- h.Value 
            | "connection" when h.Value <> "Keep-Alive" -> 
                webRequest.KeepAlive <- false
            | "if-modified-since" -> 
                let dts =  
                    match h.Value |> String.indexOfChar ';' with
                    | Some pos -> h.Value |> String.substring2 0 pos
                    | None -> h.Value
                let dt = System.DateTime.Parse (dts.Trim())
                webRequest.IfModifiedSince <- dt
            | "content-length" -> 
                match Some h.Value with
                | Int value -> webRequest.ContentLength <- int64 value
                | _ -> printf "Could not set Content-Length"
            | "content-type" -> webRequest.ContentType <- h.Value
            | "host" -> ()
            | "user-agent" -> webRequest.UserAgent <- h.Value
            | "referer" -> webRequest.Referer <- h.Value
            | _ -> 
                try
                    webRequest.Headers.Add(h.Key + ": " + h.Value)
                with
                | e -> printf "Could not redirect: %O" e

        // if (addXForwardedUri)
        //     webRequest.Headers.Add($"X-Forwarded-URI: {CreateXForwarded()}");                    

            //     webRequest.CertificateValidator(e =>
            //     {
            //         Logger.Current.Warning($"{Id} {e.Message}");
            //         e.ChainErrorDescriptions?.Perform(n =>
            //         {
            //             Logger.Current.Warning($"{Id} {n}");
            //             return true;
            //         });
            //         return false;
            //     });
            //     response = (HttpWebResponse)await webRequest.GetResponseAsync();

        match requestSession.Method with 
        | Method.Post -> 
            let bytes = requestSession.GetBytes ()
            use! requestStream = webRequest.GetRequestStreamAsync () |> Async.AwaitTask
            do! requestStream.AsyncWrite (bytes, 0, bytes.Length)
        | _ -> ()
        let response = 
            match 
                webRequest.GetResponseAsync () |> Async.AwaitTask 
                |> Async.Catch
                |> Async.RunSynchronously 
                with
            | Choice1Of2 res -> res
            | Choice2Of2 ex -> 
                match ex with
                | :? WebException as we -> we.Response
                | :? AggregateException as ae -> 
                    match ae.InnerException with
                    | :? WebException as we -> we.Response
                    | _ -> raise ex
                | _ -> raise ex
        let httpResponse = response :?> HttpWebResponse
        response.Headers.AllKeys 
        |> Array.map (fun key -> key, response.Headers.[key])
        //|> Array.filter (fun (k, v) -> System.String.Compare (k, "allow", true) <> 0 && System.String.Compare (k, "connection", true) <> 0)
        |> Array.filter (fun (k, v) -> System.String.Compare (k, "allow", true) <> 0)
        |> Array.iter (fun (k, v) -> requestSession.AddResponseHeader k v)                 
        do! requestSession.AsyncSendRaw (int httpResponse.StatusCode) httpResponse.StatusDescription (response.GetResponseStream ())
    }

//         case "range":
//             try
//             {
//                 var sizes = h.Value.Value.Split(new[] { ' ', '-', '/' }, StringSplitOptions.RemoveEmptyEntries).Skip(1)
//                     .Select(n => long.Parse(n)).ToArray();
//                 if (sizes.Length > 1)
//                     webRequest.AddRange(sizes[0], sizes[1]);
//             }
//             catch (Exception e)
//             {
//                 Logger.Current.Warning($"{Id} Error occurred in range: {e}");
//             }
//             break;
let private reverseProxyByHostRequest host targetBaseUrl (requestSession: RequestSession) =
    async {
        let urlRoot = requestSession.GetUrlRoot ()
        match urlRoot |> String.contains host with
        | true -> 
            let url = targetBaseUrl + requestSession.Url
            do! proxyRequest url requestSession
            return true
        | false -> return false
    }

let useReverseProxyByHost host targetBaseUrl = reverseProxyByHostRequest host targetBaseUrl

[<EntryPoint>] 
let main argv = 0