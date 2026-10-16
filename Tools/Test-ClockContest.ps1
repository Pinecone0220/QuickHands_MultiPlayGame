$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$editor='C:/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Data'
$output=Join-Path $projectRoot 'Temp/RouletteValidation'
New-Item -ItemType Directory -Force $output | Out-Null
$refs=Join-Path $editor 'MonoBleedingEdge/lib/mono/4.5'
& "$editor/NetCoreRuntime/dotnet.exe" "$editor/DotNetSdkRoslyn/csc.dll" -nologo -noconfig -nostdlib -target:exe "-out:$output/Clock.exe" "-r:$refs/mscorlib.dll" "-r:$refs/System.dll" "-r:$refs/System.Core.dll" "$projectRoot/Assets/Scripts/RouletteRules.cs" "$projectRoot/Assets/Scripts/DuelDirector.cs" "$projectRoot/Assets/Scripts/DuelTabletop.cs" "$projectRoot/Assets/Scripts/DuelWeapon.cs" "$projectRoot/Assets/Scripts/DuelGunplay.cs" "$projectRoot/Assets/Scripts/DuelClockContest.cs" "$projectRoot/Assets/Scripts/DuelArrowMemory.cs" "$projectRoot/Assets/Scripts/PartyGames.cs" "$PSScriptRoot/ClockContestTests.cs" "$PSScriptRoot/PartyIntegrationTests.cs" "$PSScriptRoot/NewMiniGameTests.cs" "$PSScriptRoot/ItemRulesTests.cs"
if($LASTEXITCODE -ne 0){throw 'Countdown compilation failed.'}
& "$editor/MonoBleedingEdge/bin/mono.exe" "$output/Clock.exe"
if($LASTEXITCODE -ne 0){throw 'Countdown tests failed.'}
