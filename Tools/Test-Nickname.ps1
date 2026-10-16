$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Data'
$output = Join-Path $projectRoot 'Temp/RouletteValidation'
New-Item -ItemType Directory -Force $output | Out-Null
$refs = Join-Path $editor 'MonoBleedingEdge/lib/mono/4.5'
& "$editor/NetCoreRuntime/dotnet.exe" "$editor/DotNetSdkRoslyn/csc.dll" -nologo -noconfig -nostdlib -target:exe "-out:$output/NicknameTests.exe" "-r:$refs/mscorlib.dll" "-r:$refs/System.dll" "-r:$refs/System.Core.dll" "$projectRoot/Assets/Scripts/NicknamePolicy.cs" "$PSScriptRoot/NicknameTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Nickname test compilation failed.' }
& "$editor/MonoBleedingEdge/bin/mono.exe" "$output/NicknameTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Nickname tests failed.' }

