$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
# Exercise the actual packaging/publish tail with synthetic files only.
$source=Get-Content -LiteralPath 'scripts/package.ps1' -Raw
$start=$source.IndexOf('$stagedArchive=')
if($start -lt 0){throw 'Packaging section not found'}
$publish=[scriptblock]::Create($source.Substring($start))
$probeRoot=Join-Path (Get-Location) ('artifacts/package-probe-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $probeRoot | Out-Null
function Compress-Archive {
    param($LiteralPath,$DestinationPath,$CompressionLevel)
    if($script:scenario -eq 'interrupted'){
        [IO.File]::WriteAllBytes($DestinationPath,[byte[]](80,75,3,4))
        throw 'Synthetic compression interruption'
    }
    if($script:scenario -eq 'invalid'){
        [IO.File]::WriteAllBytes($DestinationPath,[byte[]](1,2,3,4))
        return
    }
    Microsoft.PowerShell.Archive\Compress-Archive @PSBoundParameters
}
foreach($case in @('interrupted','invalid','existing','success')){
    $script:scenario=$case
    $caseRoot=Join-Path $probeRoot $case
    $stage=Join-Path $caseRoot 'new-stage'
    $output=Join-Path $stage 'Porto-win-x64'
    New-Item -ItemType Directory -Path $output | Out-Null
    $version='synthetic'
    $manifest=@{version=$version;source_commit='synthetic-no-personal-data'}
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build-info.json') -Encoding utf8NoBOM
    'synthetic payload' | Set-Content -LiteralPath (Join-Path $output 'payload.txt')
    $archive=Join-Path $caseRoot 'release.zip'
    if($case -eq 'existing'){[IO.File]::WriteAllText($archive,'preserve existing artifact')}
    $failed=$false
    try{& $publish | Out-Null}catch{$failed=$true}
    if($case -eq 'success'){
        if($failed -or !(Test-Path -LiteralPath $archive)){throw 'Valid package not published'}
        $expected=(Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant()+'  release.zip'
        if((Get-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Raw).Trim() -ne $expected){throw 'Checksum differs'}
    }elseif($case -eq 'existing'){
        if(!$failed -or [IO.File]::ReadAllText($archive) -ne 'preserve existing artifact'){throw 'Existing archive was overwritten'}
    }elseif(!$failed -or (Test-Path -LiteralPath $archive)){throw 'Incomplete ZIP acquired the release filename'}
    Write-Output "PASS package staging $case"
}
