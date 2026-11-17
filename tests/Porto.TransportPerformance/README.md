# Synthetic AFC benchmark

No phone, USB, network, pairing records or private files are accessed. All packets and payloads are generated. `AssemblyName=Porto.Tests` permits exercising the internal transport; this executable is not the ordinary test suite and is not packaged with Porto.

Build to a NEW artifact directory, then run its executable. Example from the repository root, using the already available SDK:

```powershell
.tools/dotnet-curl/dotnet.exe restore tests/Porto.TransportPerformance --configfile NuGet.Config
.tools/dotnet-curl/dotnet.exe build tests/Porto.TransportPerformance --no-restore -c Release -o artifacts/transport-new/out
$env:DOTNET_ROOT=Join-Path (Get-Location) '.tools/dotnet-curl'
& artifacts/transport-new/out/Porto.Tests.exe
```

The harness warms each variant, repeats each three times, checks SHA-256, byte counts and request counts, and exercises six failures at both 256 and 512 KiB. `../results.json` relative to the binary output contains the observations. It refuses to replace that file. Console output also includes environment, loaded DLL digest and fault checks.

Change only one variable per group: 64/256/512 KiB at fixed payload/delay, or command-write coalescing at fixed 64 KiB. The coalescing experiment is a wrapper, not a product change. Payloads are 16 and 64 MiB; each copy includes hashing but writes to Stream.Null. It does NOT measure saved-file verification, SQLite, filesystem durability, an album, USB or end-to-end import.

`Thread.Sleep(1)` is a requested synthetic wait per payload response, not a measured iPhone latency. On the recorded Windows run it actually consumed about 15 ms per response. Use `actualWaitMs`, not the requested value, when interpreting results. Zero-delay cases expose local allocation/hashing overhead. CPU and peak working set are process-level; peak is cumulative.

Recorded evidence and limits: `docs/REVIEW-RC20.md` and `docs/performance-rc20/transport-results.json`. A faster simulated wire does not prove 29 GB within one hour on hardware.
