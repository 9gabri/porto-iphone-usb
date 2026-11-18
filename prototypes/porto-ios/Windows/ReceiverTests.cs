using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Porto.Lan;

internal static class ReceiverTests
{
    static void Check(bool result, string description)
    {
        if(!result) throw new InvalidOperationException(description);
    }
    static string Digest(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    static string Id(int i)=>Digest(Encoding.UTF8.GetBytes("synthetic-"+i));
    static byte[] Data(int i)
    {
        var bytes=new byte[512+i%32];
        // First two identically named assets intentionally have identical bytes.
        new Random(Math.Max(1,i)).NextBytes(bytes);
        return i==0 ? Data(1) : bytes;
    }
    static async Task RunSession(string root, Guid device, bool resumed)
    {
        using var probe=new TcpListener(IPAddress.Loopback,0); probe.Start();
        int port=((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        bool allow=false;
        await using var server=new Receiver(root,IPAddress.Loopback,port,(_,_)=>Task.FromResult(allow),_=>{},true);
        await server.Start();
        using var http=new HttpClient(new HttpClientHandler{UseProxy=false,
            ServerCertificateCustomValidationCallback=(_,c,_,_)=>c?.GetCertHashString(HashAlgorithmName.SHA256)==server.Pin})
            {BaseAddress=new Uri($"https://127.0.0.1:{port}/porto/v1/")};
        var session=new {device, album="Due foto + video", albumId=Id(-47), selection="both",count=800};
        using var denied=await http.PostAsJsonAsync("session",session);
        Check(denied.StatusCode==HttpStatusCode.Forbidden,"Consenso necessario");
        allow=true;
        using var accepted=await http.PostAsJsonAsync("session",session); accepted.EnsureSuccessStatusCode();
        var token=(await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        async Task<int> Send(string api,int i,byte[]? body=null,string? tokenOverride=null)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post,$"{api}?asset={Id(i).ToLowerInvariant()}&version={Id(i+801)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer",tokenOverride??token);
            req.Headers.Add("X-Porto-Name",Convert.ToBase64String(Encoding.UTF8.GetBytes("Ritràtto.mov")));
            req.Headers.Add("X-Porto-SHA256",Digest(Data(i)).ToLowerInvariant());
            if(body!=null) req.Content=new ByteArrayContent(body);
            using var response=await http.SendAsync(req);
            return (int)response.StatusCode;
        }
        Check(await Send("check",0,null,"invalido")==403,"Token non valido");
        int seen=0,created=0;
        for(int i=0;i<(resumed?800:323);i++)
        {
            int status=await Send("check",i);
            if(status==204){seen++;continue;}
            Check(status==404,"Assente: "+status);
            var copied=await Send("file",i,Data(i));
            Check(copied==201,$"Nuova copia {i}, stato HTTP {copied}");
            created++;
        }
        if(!resumed)
        {
            Check(seen==0 && created==323,"Prima sessione");
            Check(await Send("file",323,new byte[32])==422,"File parziale rifiutato");
            Check(await Send("check",323)==404,"File parziale assente");
            Check(Directory.GetFiles(server.LastFolder!,"*.part").Length==0,"Scarto temporaneo");
        }
        else
        {
            Check(seen==323&&created==477,"Ripresa 323/477");
            var paths=Directory.GetFiles(server.LastFolder!,"*.mov");
            Check(paths.Length==800,"800 asset distinti sul disco");
            var expected=Enumerable.Range(0,800).Select(i=>Digest(Data(i))).Order();
            Check(paths.Select(path=>Digest(File.ReadAllBytes(path))).Order().SequenceEqual(expected),"Byte integri");
            string key=Digest(Convert.FromHexString(Id(0)+Digest(Data(0))));
            var first=paths.Single(p=>p.Contains("_"+key+"."));
            File.WriteAllBytes(first,[9,9]); // Only the newly created synthetic fixture is mutated.
            Check(await Send("check",0)==409 && await Send("file",0,Data(0))==409,"Conflitto conservato");
            Check(File.ReadAllBytes(first).SequenceEqual(new byte[]{9,9}),"Esistente conservato");
        }
    }
    public static async Task<int> Run(string parent)
    {
        var root=Directory.CreateDirectory(Path.Combine(parent,"test-album-"+Guid.NewGuid().ToString("N"))).FullName;
        var report=Path.Combine(root,"esito.txt");
        try
        {
            var device=Guid.NewGuid();
            await RunSession(root,device,false);
            await RunSession(root,device,true);
            File.WriteAllText(report,"OK. 800 asset sintetici su HTTPS loopback. Dopo riavvio: 323 riconosciuti, 477 trasferiti. Ciascun contenuto confrontato. Verificati consenso, token, duplicati omonimi, file parziali e copie corrotte conservate.");
            return 0;
        }
        catch(Exception ex){File.WriteAllText(report,ex.ToString());return 1;}
    }
}
