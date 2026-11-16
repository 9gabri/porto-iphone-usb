using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace Porto.Apple;

// Pairing certificates are exact trust anchors, not certificates discovered through a public CA.
// Apple pairing can use an empty issuer DN, rejected by BC 2.7's general X.509 parser.
// Adapt only the two already-authorized DER values; never alter wire bytes or global BC policy.
internal sealed class ApplePairingCrypto(byte[] ownCertificate, byte[] peerCertificate) : BcTlsCrypto
{
    private readonly byte[] own = ownCertificate.ToArray(), peer = peerCertificate.ToArray();

    public override TlsCertificate CreateCertificate(short type, byte[] encoding)
    {
        try { return base.CreateCertificate(type, encoding); }
        catch (TlsFatalAlert error) when (type == CertificateType.X509 && error.AlertDescription == AlertDescription.bad_certificate)
        {
            if (!CryptographicOperations.FixedTimeEquals(encoding, own) &&
                !CryptographicOperations.FixedTimeEquals(encoding, peer)) throw;

            try
            {
                using var certificate = X509CertificateLoader.LoadCertificate(encoding);
                if (!certificate.IssuerName.RawData.AsSpan().SequenceEqual(new byte[] { 0x30, 0 }) ||
                    !certificate.RawData.AsSpan().SequenceEqual(encoding)) throw new CryptographicException();
                return new PairedCertificate(this, certificate);
            }
            catch (Exception exception) when (exception is CryptographicException or ArgumentException or IOException)
            {
                // Keep parser exceptions and certificate contents out of user-facing diagnostics.
                throw new TlsFatalAlert(AlertDescription.bad_certificate);
            }
        }
    }

    private sealed class PairedCertificate : BcTlsRawKeyCertificate
    {
        private readonly byte[] original;
        private readonly Dictionary<string, byte[]> extensions = new(StringComparer.Ordinal);
        private readonly AlgorithmIdentifier signature;
        private readonly Org.BouncyCastle.Math.BigInteger serial;

        internal PairedCertificate(BcTlsCrypto crypto, X509Certificate2 certificate)
            : base(crypto, SubjectPublicKeyInfo.GetInstance(certificate.PublicKey.ExportSubjectPublicKeyInfo()))
        {
            original = certificate.RawData;
            var outer = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(original));
            if (outer.Count != 3) throw new CryptographicException();
            signature = AlgorithmIdentifier.GetInstance(outer[1]);
            _ = DerBitString.GetInstance(outer[2]);
            serial = new Org.BouncyCastle.Math.BigInteger(1, certificate.SerialNumberBytes.Span);
            foreach (var extension in certificate.Extensions)
            {
                if (extension.Oid?.Value is not string oid || !extensions.TryAdd(oid, extension.RawData))
                    throw new CryptographicException();
            }
        }

        public override byte[] GetEncoded() => original.ToArray();
        public override byte[] GetExtension(DerObjectIdentifier extensionOid) =>
            extensions.TryGetValue(extensionOid.Id, out var value) ? value.ToArray() : null!;
        public override Org.BouncyCastle.Math.BigInteger SerialNumber => serial;
        public override string SigAlgOid => signature.Algorithm.Id;
        public override Asn1Encodable GetSigAlgParams() => signature.Parameters;

        protected override bool SupportsKeyUsage(int keyUsageBits)
        {
            if (!extensions.TryGetValue(X509Extensions.KeyUsage.Id, out var encoded)) return true;
            var usage = KeyUsage.GetInstance(Asn1Object.FromByteArray(encoded)).GetBytes();
            return usage.Length > 0 && (usage[0] & keyUsageBits) == keyUsageBits;
        }
    }
}
