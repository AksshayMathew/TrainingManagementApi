<#
.SYNOPSIS
  Points the API at the Docker SQL Server from docker-compose.yml (no password typing, nothing tracked by Git).

.DESCRIPTION
  Reads MSSQL_SA_PASSWORD from the git-ignored .env file in the repository root and stores a matching
  connection string in your personal user-secrets store for this project. Run it once per machine/user account,
  from a normal PowerShell window (the same account that runs `dotnet run`):

      powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\use-docker-sqlserver.ps1

  Use `dotnet user-secrets clear --project TrainingManagementApi` to go back to the default LocalDB connection string.
#>
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repoRoot '.env'

if (-not (Test-Path -LiteralPath $envFile)) {
    throw "File not found: $envFile. Copy .env.example to .env and set MSSQL_SA_PASSWORD first."
}

$line = Get-Content -LiteralPath $envFile | Where-Object { $_ -match '^\s*MSSQL_SA_PASSWORD\s*=' } | Select-Object -First 1
if (-not $line) { throw "MSSQL_SA_PASSWORD is missing in $envFile." }

$password = ($line -split '=', 2)[1].Trim()
if (-not $password) { throw "MSSQL_SA_PASSWORD in $envFile is empty." }

# 127.0.0.1 (not localhost): the container is published on the IPv4 loopback address only.
$connection = "Server=127.0.0.1,1433;Database=TrainingManagementDb;User Id=sa;Password=$password;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true"

& dotnet user-secrets set 'ConnectionStrings:DefaultConnection' $connection --project (Join-Path $repoRoot 'TrainingManagementApi') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'dotnet user-secrets failed (is the .NET 8 SDK installed?).' }

Write-Host 'Done: the API will now use the Docker SQL Server (127.0.0.1,1433) when started with `dotnet run`.' -ForegroundColor Green
Write-Host 'Make sure the container is running: docker compose up -d   (wait ~30 seconds the first time).'
