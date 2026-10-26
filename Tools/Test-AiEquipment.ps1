$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Data'
$output = Join-Path $projectRoot 'Temp/RouletteValidation'
New-Item -ItemType Directory -Force $output | Out-Null
$refs = Join-Path $editor 'MonoBleedingEdge/lib/mono/4.5'
$sources = @('RouletteRules','DuelDirector','DuelDirector.Practice','DuelDirector.Ink','DuelDirector.QuickItems','DuelDirector.Bullets','DuelDirector.NoruFace','DuelDirector.AiEquipment','DuelInk','DuelLoadout','DuelDiceContest','DuelTabletop','DuelWeapon','DuelGunplay','DuelClockContest','DuelArrowMemory','PartyGames') | ForEach-Object { Join-Path $projectRoot "Assets/Scripts/$_.cs" }
& "$editor/NetCoreRuntime/dotnet.exe" "$editor/DotNetSdkRoslyn/csc.dll" -nologo -noconfig -nostdlib -target:exe "-out:$output/AiEquipment.exe" "-r:$refs/mscorlib.dll" "-r:$refs/System.dll" "-r:$refs/System.Core.dll" @sources "$PSScriptRoot/AiEquipmentTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'AI equipment compilation failed.' }
& "$editor/MonoBleedingEdge/bin/mono.exe" "$output/AiEquipment.exe"
if ($LASTEXITCODE -ne 0) { throw 'AI equipment tests failed.' }
