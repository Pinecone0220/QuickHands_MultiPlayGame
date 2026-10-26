param(
    [Parameter(Mandatory=$true)][string]$QaRoot,
    [Parameter(Mandatory=$true)][string]$PythonPath,
    [Parameter(Mandatory=$true)][string]$CaseId,
    [Parameter(Mandatory=$true)][int]$DelayMs,
    [Parameter(Mandatory=$true)][int]$PortBase,
    [switch]$Spike
)
$runDir=Join-Path $QaRoot $CaseId
if(Test-Path -LiteralPath $runDir){throw 'Use a fresh CaseId to avoid stale test markers.'}
New-Item -ItemType Directory -Path $runDir | Out-Null
$proxyScript=Join-Path $PSScriptRoot 'UDPDelayProxy.py'
$proxyArgs=@(('"'+$proxyScript+'"'),'--listen',($PortBase+1),'--target',$PortBase,'--delay',$DelayMs,
    '--stop',('"'+$runDir+'/proxy.stop"'),'--stats',('"'+$runDir+'/proxy.json"'))
if($Spike){$proxyArgs+=@('--switch-marker',('"'+$runDir+'/spike-hidden"'))}
$proxy=Start-Process -FilePath $PythonPath -ArgumentList $proxyArgs -WindowStyle Hidden -RedirectStandardError "$runDir/proxy.err" -PassThru
$records=@(@{role='proxy';pid=$proxy.Id})
$clientRole=if($Spike){'spike'}else{'client'}
foreach($role in @('host',$clientRole))
{
    $port=if($role -eq 'host'){$PortBase}else{$PortBase+1}
    $proc=Start-Process -FilePath "$QaRoot/Build/ChatQA.exe" -ArgumentList @('-batchmode','-screen-width','640','-screen-height','480',
        '--ping-clock-qa',$role,'--ping-dir',('"'+$runDir+'"'),'--ping-peers','1','--ping-port',$port,
        '-logFile',('"'+$runDir+'/'+$role+'.log"')) -WindowStyle Hidden -PassThru
    $records+=@{role=$role;pid=$proc.Id}
}
$records | ConvertTo-Json | Set-Content -LiteralPath "$runDir/processes.json"
Write-Output "$CaseId launched (one-way artificial delay $DelayMs ms)."
