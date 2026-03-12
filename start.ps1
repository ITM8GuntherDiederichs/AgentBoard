# start.ps1 — Reliable AgentBoard launcher
# Kills any existing dotnet process locking the DLL, then starts fresh.

$dll = "$PSScriptRoot\AgentBoard\bin\Debug\net10.0\AgentBoard.dll"
$port = 5227

Write-Host "Checking for processes on port $port..."
$procs = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess | Sort-Object -Unique
foreach ($pid in $procs) {
    Write-Host "Stopping PID $pid (using port $port)"
    Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
}

# Also kill any dotnet process holding the DLL
Get-Process -Name dotnet -ErrorAction SilentlyContinue | ForEach-Object {
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}

Start-Sleep -Seconds 1

Write-Host "Building..."
Set-Location "$PSScriptRoot\AgentBoard"
dotnet build --configuration Debug -v q
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed"; exit 1 }

$env:ASPNETCORE_ENVIRONMENT = "Development"
Write-Host "Starting AgentBoard on http://localhost:$port ..."
dotnet exec $dll --urls "http://localhost:$port"
