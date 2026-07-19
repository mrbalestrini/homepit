Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$validationId = [guid]::NewGuid().ToString("N").Substring(0, 10)
$pgContainer = "organiza-club-validation-pg-$validationId"
$minioContainer = "organiza-club-validation-minio-$validationId"
$apiProcess = $null
$stdoutPath = Join-Path ([System.IO.Path]::GetTempPath()) "$pgContainer-api.out.log"
$stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) "$pgContainer-api.err.log"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Get-FreePort {
  $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
  $listener.Start()
  try {
    return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
  }
  finally {
    $listener.Stop()
  }
}

$pgPort = Get-FreePort
$minioPort = Get-FreePort
$apiPort = Get-FreePort

try {
  docker info *> $null
  if ($LASTEXITCODE -ne 0) { throw "Docker daemon indisponivel." }

  docker run --rm -d --name $pgContainer -e POSTGRES_PASSWORD=validation_only_password -p "127.0.0.1:${pgPort}:5432" postgres:16-alpine | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "Falha ao iniciar PostgreSQL descartavel." }

  docker run --rm -d --name $minioContainer -e MINIO_ROOT_USER=validationaccess -e MINIO_ROOT_PASSWORD=validationsecret123 -p "127.0.0.1:${minioPort}:9000" minio/minio:latest server /data | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "Falha ao iniciar MinIO descartavel." }

  $pgReady = $false
  for ($attempt = 0; $attempt -lt 45; $attempt += 1) {
    docker exec $pgContainer pg_isready -U postgres -d postgres *> $null
    if ($LASTEXITCODE -eq 0) { $pgReady = $true; break }
    Start-Sleep -Milliseconds 1000
  }
  if (-not $pgReady) { throw "PostgreSQL descartavel nao ficou pronto." }

  $minioReady = $false
  for ($attempt = 0; $attempt -lt 45; $attempt += 1) {
    try {
      $response = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$minioPort/minio/health/live" -TimeoutSec 2
      if ($response.StatusCode -eq 200) { $minioReady = $true; break }
    }
    catch {}
    Start-Sleep -Milliseconds 1000
  }
  if (-not $minioReady) { throw "MinIO descartavel nao ficou pronto." }

  $env:ASPNETCORE_ENVIRONMENT = "Development"
  $env:ASPNETCORE_URLS = "http://127.0.0.1:$apiPort"
  $env:ConnectionStrings__OrganizaClubDb = "Host=127.0.0.1;Port=$pgPort;Database=postgres;Username=postgres;Password=validation_only_password"
  $env:Database__ApplyMigrationsOnStartup = "true"
  $env:Jwt__Issuer = "organiza-club-validation"
  $env:Jwt__Audience = "organiza-club-validation"
  $env:Jwt__SigningKey = "organiza-club-validation-signing-key-123456789"
  $env:ObjectStorage__Endpoint = "http://127.0.0.1:$minioPort"
  $env:ObjectStorage__AccessKey = "validationaccess"
  $env:ObjectStorage__SecretKey = "validationsecret123"
  $env:ObjectStorage__BucketName = "organiza-club-assets"
  $env:ObjectStorage__UseSsl = "false"
  $env:ObjectStorage__CreateBucketOnStartup = "true"
  $env:Integrations__Enabled = "false"
  $env:Mcp__Enabled = "false"

  $apiDll = (Resolve-Path (Join-Path $repoRoot "apps\api\src\OrganizaClub.Api\bin\Release\net10.0\publish\OrganizaClub.Api.dll")).Path
  $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList @($apiDll) -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath

  $apiReady = $false
  for ($attempt = 0; $attempt -lt 60; $attempt += 1) {
    if ($apiProcess.HasExited) { break }
    try {
      $response = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$apiPort/health" -TimeoutSec 2
      if ($response.StatusCode -eq 200) { $apiReady = $true; break }
    }
    catch {}
    Start-Sleep -Milliseconds 1000
  }
  if (-not $apiReady) {
    $apiLogs = if (Test-Path -LiteralPath $stderrPath) { Get-Content -Raw -LiteralPath $stderrPath } else { "" }
    throw "API descartavel nao ficou pronta. $apiLogs"
  }

  $schema = (docker exec $pgContainer psql -U postgres -d postgres -tAc "SELECT schema_name FROM information_schema.schemata WHERE schema_name='organiza_club';").Trim()
  $historyCount = (docker exec $pgContainer psql -U postgres -d postgres -tAc 'SELECT count(*) FROM organiza_club."__EFMigrationsHistory";').Trim()
  $tableCount = (docker exec $pgContainer psql -U postgres -d postgres -tAc "SELECT count(*) FROM information_schema.tables WHERE table_schema='organiza_club';").Trim()
  if ($schema -ne "organiza_club" -or [int]$historyCount -ne 1 -or [int]$tableCount -lt 20) {
    throw "A baseline descartavel nao produziu o schema esperado."
  }

  docker run --rm --entrypoint /bin/sh minio/mc -c "mc alias set validation http://host.docker.internal:$minioPort validationaccess validationsecret123 >/dev/null && mc stat validation/organiza-club-assets >/dev/null" *> $null
  if ($LASTEXITCODE -ne 0) { throw "O bucket organiza-club-assets nao foi criado no MinIO vazio." }

  Write-Host "Disposable startup: ok; schema=$schema; migrations=$historyCount; tables=$tableCount; bucket=organiza-club-assets"
}
finally {
  if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
    Stop-Process -Id $apiProcess.Id -Force
  }

  foreach ($container in @($minioContainer, $pgContainer)) {
    $resolvedName = docker inspect --format "{{.Name}}" $container 2>$null
    if ($LASTEXITCODE -eq 0 -and $resolvedName.TrimStart("/") -eq $container) {
      docker stop --time 2 $container *> $null
    }
  }

  foreach ($logPath in @($stdoutPath, $stderrPath)) {
    if (Test-Path -LiteralPath $logPath) {
      Remove-Item -LiteralPath $logPath -Force
    }
  }
}
