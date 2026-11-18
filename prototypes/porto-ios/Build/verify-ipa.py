"""Check the unsigned-for-device prototype archive on Windows or macOS."""
import hashlib
import plistlib
import struct
import sys
import zipfile
from pathlib import Path


def verify(path: Path) -> None:
    prefix = "Payload/PortoAlbum.app/"
    with zipfile.ZipFile(path) as archive:
        members = [item.filename for item in archive.infolist() if not item.is_dir()]
        expected = {prefix + name for name in ("Info.plist", "PortoAlbum", "_CodeSignature/CodeResources")}
        if set(members) != expected or len(members) != len(expected):
            raise ValueError("Il contenuto dell'IPA non corrisponde al bundle previsto.")
        plist = plistlib.loads(archive.read(prefix + "Info.plist"))
        if any(not plist.get(key) for key in (
            "CFBundleIdentifier", "CFBundleVersion", "CFBundleShortVersionString", "NSPhotoLibraryUsageDescription",
            "NSLocalNetworkUsageDescription",
        )):
            raise ValueError("Metadati o descrizioni dei permessi mancanti.")
        if (plist.get("CFBundleExecutable"), plist.get("CFBundlePackageType"), plist.get("MinimumOSVersion"),
                plist.get("CFBundleSupportedPlatforms")) != ("PortoAlbum", "APPL", "17.0", ["iPhoneOS"]):
            raise ValueError("Metadati incoerenti con la build per iPhone.")
        member = archive.getinfo(prefix + "PortoAlbum")
        if not ((member.external_attr >> 16) & 0o111):
            raise ValueError("Attributo eseguibile assente.")
        image = archive.read(member)
        if len(image) < 32:
            raise ValueError("Eseguibile troncato.")
        magic, cpu, _, kind, commands, cmdbytes, _, _ = struct.unpack_from("<8I", image)
        if (magic, cpu, kind) != (0xFEEDFACF, 0x0100000C, 2):
            raise ValueError("Il binario deve essere un eseguibile Mach-O arm64.")
        pos, end = 32, 32 + cmdbytes
        if end > len(image):
            raise ValueError("Tabella dei comandi troncata.")
        device_build = False
        for _ in range(commands):
            command, length = struct.unpack_from("<II", image, pos)
            if length < 8 or pos + length > end:
                raise ValueError("Comando Mach-O non valido.")
            if command == 0x32 and length >= 24:  # LC_BUILD_VERSION
                platform, minimum = struct.unpack_from("<II", image, pos + 8)
                device_build = (platform, minimum) == (2, 17 << 16)
            pos += length
        if not device_build or pos != end or archive.testzip() is not None:
            raise ValueError("Piattaforma, versione minima o integrità ZIP errata.")
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    print(f"IPA verificata: iPhone arm64, iOS 17.0+, SHA256 {digest.hexdigest()}")


if __name__ == "__main__":
    verify(Path(sys.argv[1]))
