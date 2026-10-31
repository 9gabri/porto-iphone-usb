$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$tag=git describe --tags --exact-match HEAD
if($LASTEXITCODE -ne 0 -or $tag -notmatch '^v([0-9]+\.[0-9]+\.[0-9]+-rc\.[0-9]+)$'){throw 'A release candidate tag is required'}
$version=$Matches[1]
[xml]$props=Get-Content -LiteralPath 'Directory.Build.props'
if($props.Project.PropertyGroup.Version -ne $version){throw 'Tag and application version differ'}
if((git status --porcelain)){throw 'Commit all source changes before packaging'}
$archive=Join-Path $root "artifacts/Porto-$version-win-x64.zip"
if(Test-Path -LiteralPath $archive){throw 'Archive already exists; preserve release artifacts'}
# Each release gets a new empty staging directory; ignored files from older builds cannot leak.
$stage=Join-Path $root ('artifacts/package-'+$version+'-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -ErrorAction Stop | Out-Null
$output=Join-Path $stage 'Porto-win-x64'
& "$PSScriptRoot/dev.ps1" publish -Output $output
foreach($name in @('LEGGIMI.txt','LICENSE','THIRD-PARTY-NOTICES.md')){Copy-Item -LiteralPath $name -Destination $output}
Copy-Item -LiteralPath '.tools/packages/microsoft.netcore.app.runtime.win-x64/10.0.14/LICENSE.TXT' -Destination "$output/LICENSE-DOTNET.txt"
Copy-Item -LiteralPath '.tools/packages/microsoft.netcore.app.runtime.win-x64/10.0.14/THIRD-PARTY-NOTICES.TXT' -Destination "$output/THIRD-PARTY-DOTNET.txt"
Copy-Item -LiteralPath '.tools/packages/microsoft.windowsdesktop.app.runtime.win-x64/10.0.14/LICENSE' -Destination "$output/LICENSE-WINDOWSDESKTOP.txt"
Copy-Item -LiteralPath 'docs/TESTS.md' -Destination "$output/TESTS.md"
Copy-Item -LiteralPath 'docs/test-results.txt' -Destination "$output/test-results.txt"
Copy-Item -LiteralPath 'docs/SOURCES.md' -Destination "$output/SOURCES.md"
Copy-Item -LiteralPath 'docs/ARCHITECTURE.md' -Destination "$output/ARCHITECTURE.md"
$manifest=[ordered]@{name='Porto';version=$version;source_commit=(git rev-parse HEAD);runtime='10.0.14';target='win-x64';signed=$false;hardware_tested=$false}
$manifest | ConvertTo-Json | Set-Content -LiteralPath "$output/build-info.json" -Encoding utf8NoBOM
$exe=Join-Path $output 'Porto.exe'
foreach($mode in @('--interop-smoke','--worker-smoke')){
    $p=Start-Process -FilePath $exe -ArgumentList $mode -WindowStyle Hidden -PassThru
    if(!$p.WaitForExit(15000)){Stop-Process -Id $p.Id;throw "$mode timeout"}
    if($p.ExitCode -ne 0){throw "$mode failed"}
}
$image=Join-Path $root 'artifacts/package-ui.png'
foreach($imageName in @('package-ui.png','package-ui-compact-retry.png','package-ui-compact-paused.png')){
    $image=Join-Path $root ('artifacts/'+$imageName)
    $p=Start-Process -FilePath $exe -ArgumentList @('--ui-smoke',('"'+$image+'"')) -WindowStyle Hidden -PassThru
    if(!$p.WaitForExit(15000)){Stop-Process -Id $p.Id;throw 'UI timeout'}
    if($p.ExitCode -ne 0){throw 'UI failed'}
}
# A failed compression stays inside this attempt's staging directory. It must not
# occupy the immutable release filename and prevent a subsequent clean attempt.
$stagedArchive=Join-Path $stage (Split-Path $archive -Leaf)
Compress-Archive -LiteralPath $output -DestinationPath $stagedArchive -CompressionLevel Optimal
$zip=[System.IO.Compression.ZipFile]::OpenRead($stagedArchive)
try {
    $entry=$zip.GetEntry('Porto-win-x64/build-info.json')
    if(!$entry){throw 'Missing package manifest'}
    $reader=[System.IO.StreamReader]::new($entry.Open())
    try {$packed=$reader.ReadToEnd() | ConvertFrom-Json} finally {$reader.Dispose()}
    if($packed.version -ne $version -or $packed.source_commit -ne $manifest.source_commit){throw 'Package manifest mismatch'}
    foreach($entry in $zip.Entries){$stream=$entry.Open();try{$stream.CopyTo([System.IO.Stream]::Null)}finally{$stream.Dispose()}}
} finally {$zip.Dispose()}
$hash=(Get-FileHash -LiteralPath $stagedArchive -Algorithm SHA256).Hash.ToLowerInvariant()
$checksum=Join-Path $stage 'SHA256SUMS.txt'
"$hash  $(Split-Path $archive -Leaf)" | Set-Content -LiteralPath $checksum -Encoding ascii
[System.IO.File]::Move($stagedArchive,$archive)
Write-Output "Packaged $(Split-Path $archive -Leaf)"
Write-Output "SHA256 $hash"
Write-Output "Checksum file: $checksum"
