namespace Faaz

[<RequireQualifiedAccess>]
module Http =
    open System.Net.Http
    
    let getAsync (url: string) = async {
        use http = new HttpClient()
        let! response = http.GetAsync url |> Async.AwaitTask
        let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        
        match response.IsSuccessStatusCode with
        | true -> return Ok content
        | false -> return Error content
    }
    
    let get url = getAsync url |> Async.RunSynchronously

