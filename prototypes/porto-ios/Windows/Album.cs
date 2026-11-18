using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Porto.Lan;

internal sealed class BadInput(int status, string message) : Exception(message)
{
    public int Status => status;
}

internal sealed record Incoming(string Asset, string Revision, string Name, string Expected)
{
    static string Hex(string input)
    {
        if (input.Length != 64 || !input.All(char.IsAsciiHexDigit))
            throw new BadInput(400, "Identità del file o impronta non valida.");
        return input.ToUpperInvariant();
    }

    public static Incoming Read(HttpRequest req)
    {
        var id = Hex(req.Query["asset"].ToString());
        var revision = Hex(req.Query["version"].ToString());
        var digest = Hex(req.Headers["X-Porto-SHA256"].ToString());
        string name;
        try { name = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(req.Headers["X-Porto-Name"].ToString())); }
        catch (Exception e) when (e is FormatException or DecoderFallbackException)
        { throw new BadInput(400, "Nome file non valido."); }
        if (name.Length is 0 or > 255 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new BadInput(400, "Nome file non valido.");
        return new(id, revision, name, digest);
    }
}

internal sealed class Album : IDisposable
{
    public string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string Peer { get; }
    public string DirectoryPath { get; }
    readonly Dictionary<string, Incoming> confirmed = new(StringComparer.Ordinal);
    readonly int count;
    readonly Action<string> notify;
    readonly FileStream report;
    int created;
    static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".heic", ".heif", ".hif", ".mov", ".mp4", ".m4v", ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".dng",
          ".gif", ".webp", ".bmp", ".avif", ".mkv", ".avi", ".3gp", ".3g2", ".mts", ".m2ts", ".mpg", ".mpeg", ".jxl" };

    public Album(string root, Guid device, string albumId, string album, string selection, int count, string peer, Action<string> notify)
    {
        this.notify = notify;
        this.count = count;
        Peer = peer;
        var namespaceBytes = JsonSerializer.SerializeToUtf8Bytes(new[] {device.ToString("D"), albumId.ToUpperInvariant(), selection});
        var suffix = Convert.ToHexString(SHA256.HashData(namespaceBytes))[..24];
        var title = Destination.SafeStem(album + ".album")[2..];
        DirectoryPath = Path.Combine(root, $"{title} - {suffix}");
        Destination.CheckAncestors(DirectoryPath);
        Directory.CreateDirectory(DirectoryPath);
        report = new FileStream(Path.Combine(DirectoryPath, $"invio-{Guid.NewGuid():N}.jsonl"),
            FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        try { Record(new { fase = "inizio", album, selection, count }); }
        catch { report.Dispose(); throw; }
    }

    void Record(object row)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(row);
        report.Write(bytes);
        report.WriteByte(10);
        report.Flush(true);
    }

    string PathFor(Incoming file)
    {
        if (confirmed.TryGetValue(file.Asset, out var previous) && previous != file)
            throw new BadInput(409, "Un elemento è cambiato durante l’invio. Riapri l’album per verificare la nuova versione.");
        if (previous == null && confirmed.Count >= count)
            throw new BadInput(409, "Numero di elementi autorizzato superato.");
        string ext = Path.GetExtension(file.Name).ToLowerInvariant();
        if (!MediaExtensions.Contains(ext)) throw new BadInput(415, "Estensione foto o video non supportata.");
        var key = SHA256.HashData(Convert.FromHexString(file.Asset + file.Expected));
        string name = $"{Destination.SafeStem(file.Name)}_{Convert.ToHexString(key)}{ext}";
        var path = Path.Combine(DirectoryPath, name);
        Destination.CheckAncestors(path);
        return path;
    }

    static async Task<bool> Matches(string path, string expected, CancellationToken token)
    {
        Destination.CheckAncestors(path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)) == expected;
    }

    void Confirm(Incoming file, string path, bool isNew)
    {
        if (confirmed.ContainsKey(file.Asset)) return;
        Record(new { fase = isNew ? "salvato" : "gia_presente", file.Asset, file.Revision, file.Name,
            sha256 = file.Expected, filePC = Path.GetFileName(path) });
        confirmed.Add(file.Asset, file);
        if (isNew) created++;
        notify($"{confirmed.Count}/{count} file verificati · {created} nuovi · {confirmed.Count-created} già presenti");
    }

    public async Task<int> Check(Incoming file, CancellationToken token)
    {
        var path = PathFor(file);
        if (!File.Exists(path)) return 404;
        if (!await Matches(path, file.Expected, token))
            throw new BadInput(409, "Una copia precedente ha contenuto diverso. Conservata per il controllo.");
        Confirm(file, path, false);
        return 204;
    }

    public async Task<int> Receive(Incoming file, Stream input, long? length, CancellationToken token)
    {
        const long maximum = 256L << 30;
        if (length is not (> 0 and <= maximum)) throw new BadInput(411, "Serve la lunghezza del file, fino a 256 GiB.");
        var path = PathFor(file);
        if (new DriveInfo(Path.GetPathRoot(path)!).AvailableFreeSpace < length.Value)
            throw new BadInput(507, "Spazio su disco insufficiente.");
        var part = Path.Combine(DirectoryPath, $".porto-{Guid.NewGuid():N}.part");
        var buffer = ArrayPool<byte>.Shared.Rent(256 * 1024);
        bool owns = false;
        try
        {
            long size = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var target = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                owns = true;
                int n;
                while ((n = await input.ReadAsync(buffer.AsMemory(), token)) != 0)
                {
                    size += n;
                    if (size > length.Value) throw new BadInput(422, "Troppi byte ricevuti.");
                    hash.AppendData(buffer, 0, n);
                    await target.WriteAsync(buffer.AsMemory(0,n), token);
                }
                if (size != length.Value || Convert.ToHexString(hash.GetHashAndReset()) != file.Expected)
                    throw new BadInput(422, "File incompleto o impronta diversa dall’originale.");
                target.Flush(true);
            }
            if (!await Matches(part, file.Expected, token)) throw new BadInput(422, "Verifica sul disco fallita.");
            token.ThrowIfCancellationRequested();
            if (File.Exists(path)) return await Check(file, token);
            Destination.Publish(part, path);
            owns = false;
            Confirm(file, path, true);
            return 201;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (owns) File.Delete(part);
        }
    }
    public void Dispose() => report.Dispose();
}
