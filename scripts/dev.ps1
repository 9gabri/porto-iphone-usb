param([ValidateSet('test','build','publish')][string]$Task='test',[switch]$Large,[string]$Output='artifacts/Porto-win-x64')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$env:DOTNET_CLI_HOME=Join-Path $root '.tools/cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$sdk=Join-Path $root '.tools/dotnet-curl/dotnet.exe'
if(!(Test-Path -LiteralPath $sdk)){$sdk='dotnet'}
$project=if($Task -eq 'test'){'tests/Porto.Tests'}else{'src/Porto.App'}
& $sdk restore $project --configfile NuGet.Config
if($LASTEXITCODE -ne 0){throw 'Restore failed'}
if($Task -eq 'test'){
    if($Large){& $sdk run --project $project --no-restore -c Release -- --large}
    else{& $sdk run --project $project --no-restore -c Release}
}
elseif($Task -eq 'build'){& $sdk build $project --no-restore -c Release}
else{& $sdk publish $project -c Release -r win-x64 --self-contained true --configfile NuGet.Config -p:DebugType=None -p:DebugSymbols=false -o $Output}
if($LASTEXITCODE -ne 0){throw "$Task failed"}
