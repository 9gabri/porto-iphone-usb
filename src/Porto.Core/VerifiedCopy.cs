using System.Buffers;
using System.Security.Cryptography;

namespace Porto.Core;

public static class VerifiedCopy
{
    public const int BufferSize = 1024 * 1024;
    public const long ReserveBytes = 64L * 1024 * 1024;

    public static CopyResult Copy(Stream input, Stream output, long? expected, Action<long>? progress,
        Action? checkSpace, CancellationToken cancel)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long total = 0;
        try
        {
            while (true)
            {
                cancel.ThrowIfCancellationRequested();
                checkSpace?.Invoke();
                int read = input.Read(buffer, 0, BufferSize);
                if (read == 0) break;
                total = checked(total + read);
                if (expected.HasValue && total > expected.Value)
                    throw new InvalidDataException("La sorgente supera la dimensione dichiarata; copia non pubblicata.");
                output.Write(buffer, 0, read);
                hash.AppendData(buffer, 0, read);
                progress?.Invoke(total);
            }
            if (expected.HasValue && total != expected.Value)
                throw new InvalidDataException("Dimensione diversa da quella dichiarata dalla sorgente.");
            return new(total, Convert.ToHexString(hash.GetHashAndReset()));
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    public static CopyResult Hash(Stream input, CancellationToken cancel, Action<long>? progress = null) =>
        Copy(input, Stream.Null, null, progress, null, cancel);

    public static CopyResult HashFile(string path, CancellationToken cancel, Action<long>? progress = null)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.SequentialScan);
        return Hash(file, cancel, progress);
    }

    public static void Verify(CopyResult received, CopyResult saved)
    {
        if (received != saved) throw new InvalidDataException("Verifica SHA-256 fallita. Il file non viene pubblicato.");
    }
}
