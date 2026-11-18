using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Porto.Lan;

internal sealed class Receiver : IAsyncDisposable
{
    readonly WebApplication app;
    readonly X509Certificate2 certificate;
    readonly string root;
    readonly Func<string, CancellationToken, Task<bool>> approve;
    readonly Action<string> notify;
    readonly SemaphoreSlim work = new(1);
    readonly CancellationTokenSource stop = new();
    Album? album;
    public string? LastFolder => album?.DirectoryPath;
    public string Pin => certificate.GetCertHashString(HashAlgorithmName.SHA256);
    public string Pair { get; }

    static bool Private(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b=ip.GetAddressBytes();
        return b[0] == 10 || b[0] == 172 && b[1] is >=16 and <=31 || b[0] == 192 && b[1] == 168;
    }

    static IEnumerable<UnicastIPAddressInformation> Adapters() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus==OperationalStatus.Up && n.NetworkInterfaceType!=NetworkInterfaceType.Loopback)
        .OrderBy(n => n.NetworkInterfaceType==NetworkInterfaceType.Wireless80211 ? 0 : 1)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses).Where(a => Private(a.Address));

    public static IEnumerable<IPAddress> Addresses() => Adapters().Select(a=>a.Address).Distinct();

    static System.Net.IPNetwork Subnet(IPAddress ip)
    {
        var adapter = Adapters().FirstOrDefault(a=>a.Address.Equals(ip)) ?? throw new IOException("Scegli la rete privata del PC.");
        var bytes=ip.GetAddressBytes();
        var mask=adapter.IPv4Mask.GetAddressBytes();
        for (int i=0;i<bytes.Length;i++) bytes[i] &= mask[i];
        return new(new IPAddress(bytes),adapter.PrefixLength);
    }

    public Receiver(string root, IPAddress ip, int port, Func<string,CancellationToken,Task<bool>> approve,
                    Action<string> notify, bool testing = false)
    {
        this.root = Path.GetFullPath(root);
        this.notify=notify; this.approve=approve;
        if (testing && !IPAddress.IsLoopback(ip)) throw new ArgumentException("Il test usa solo loopback.");
        var subnet = testing ? new System.Net.IPNetwork(ip,32) : Subnet(ip);
        var drive = new DriveInfo(Path.GetPathRoot(this.root)!);
        if (!Directory.Exists(this.root) || drive.DriveType!=DriveType.Fixed || drive.DriveFormat!="NTFS")
            throw new IOException("Scegli una cartella locale su disco NTFS.");
        Destination.CheckAncestors(this.root);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Porto Album",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(ip);
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") },false));
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddDays(2));
        certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx),null);
        Pair = $"porto://ricevi?ip={ip}&pin={Pin}";
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args=[], ContentRootPath=AppContext.BaseDirectory });
        builder.Configuration.Sources.Clear(); builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.AddServerHeader=false;
            o.Limits.MaxConcurrentConnections=12;
            o.Limits.MaxRequestBodySize=8192;
            o.Listen(ip,port,l=>l.UseHttps(certificate));
        });
        app=builder.Build();
        app.Use(async (ctx,next)=>
        {
            if(ctx.Connection.RemoteIpAddress is not { } address || !subnet.Contains(address))
            { ctx.Response.StatusCode=403; return; }
            try { await next(ctx); }
            catch (BadInput ex) { ctx.Response.StatusCode=ex.Status; notify(ex.Message); }
            catch (Exception ex) when (ex is JsonException or Microsoft.AspNetCore.Http.BadHttpRequestException)
            { ctx.Response.StatusCode=400; }
            catch (OperationCanceledException) { ctx.Response.StatusCode=409; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { ctx.Response.StatusCode=500; notify("Ricezione interrotta. Verifica il disco del PC e riprova dall’iPhone."); }
        });
        app.MapPost("/porto/v1/session",(Func<HttpContext,Task<IResult>>)Session);
        app.MapMethods("/porto/v1/check",["GET","POST"],(Func<HttpContext,Task<IResult>>)(ctx=>FileRequest(ctx,false)));
        app.MapPost("/porto/v1/file",(Func<HttpContext,Task<IResult>>)(ctx=>FileRequest(ctx,true)));
    }

    sealed record Offer(string? Device, string? AlbumId, string? Album, string? Selection, int Count);
    async Task<IResult> Session(HttpContext ctx)
    {
        if (!await work.WaitAsync(0)) return Results.StatusCode(409);
        try
        {
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop.Token,ctx.RequestAborted);
            deadline.CancelAfter(TimeSpan.FromMinutes(2));
            var offer=await JsonSerializer.DeserializeAsync<Offer>(ctx.Request.Body,
                JsonSerializerOptions.Web,deadline.Token);
            if (offer?.Album is not { Length: >0 and <=512 } title || title.Any(char.IsControl)
                || !Guid.TryParse(offer.Device,out var device) || offer.Count is <1 or >100_000
                || offer.AlbumId is not {Length:64} id || !id.All(char.IsAsciiHexDigit)
                || offer.Selection is not ("photo" or "video" or "both")) return Results.BadRequest();
            var peer=ctx.Connection.RemoteIpAddress!.ToString();
            if(!await approve($"L’iPhone all’indirizzo {peer} propone l’album «{title}» ({offer.Count} elementi).",deadline.Token))
                return Results.StatusCode(403);
            deadline.Token.ThrowIfCancellationRequested();
            var replacement = new Album(root, device, id, title, offer.Selection, offer.Count, peer, notify);
            album?.Dispose();
            album=replacement;
            notify($"Pronto a ricevere {offer.Count} elementi dall’album «{title}».");
            return Results.Json(new {token=album.Token});
        }
        finally { work.Release(); }
    }

    async Task<IResult> FileRequest(HttpContext ctx,bool receive)
    {
        if (!await work.WaitAsync(0)) return Results.StatusCode(409);
        try
        {
            if (album == null || album.Peer != ctx.Connection.RemoteIpAddress?.ToString()
                || ctx.Request.Headers.Authorization.ToString() != "Bearer "+album.Token) return Results.StatusCode(403);
            var file=Incoming.Read(ctx.Request);
            using var life=CancellationTokenSource.CreateLinkedTokenSource(stop.Token,ctx.RequestAborted);
            if(!receive) return Results.StatusCode(await album.Check(file,life.Token));
            var feature=ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if(feature != null) feature.MaxRequestBodySize=256L << 30;
            return Results.StatusCode(await album.Receive(file,ctx.Request.Body,ctx.Request.ContentLength,life.Token));
        }
        finally { work.Release(); }
    }

    public Task Start()=>app.StartAsync();
    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        await app.StopAsync(CancellationToken.None);
        await app.DisposeAsync();
        album?.Dispose();
        certificate.Dispose(); work.Dispose(); stop.Dispose();
    }
}
