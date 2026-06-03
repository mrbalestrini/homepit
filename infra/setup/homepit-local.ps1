param(
  [ValidateSet("start", "stop", "destroy")]
  [string]$Action,

  [ValidateSet("auto", "docker", "podman")]
  [string]$Engine = "auto",

  [switch]$Yes,

  [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:NetworkName = "homepit_net"
$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$script:ComposeTargets = @(
  @{
    Name = "Supabase"
    Project = "homepit-supabase"
    Directory = Join-Path $script:RepoRoot "infra\supabase"
    File = "docker-compose.yml"
    Build = $false
  },
  @{
    Name = "Evolution API"
    Project = "homepit-evolution"
    Directory = Join-Path $script:RepoRoot "infra\evolution"
    File = "docker-compose.yml"
    Build = $false
  },
  @{
    Name = "MinIO"
    Project = "homepit-minio"
    Directory = Join-Path $script:RepoRoot "infra\minio"
    File = "docker-compose.yml"
    Build = $false
  },
  @{
    Name = "HomePit API"
    Project = "homepit-api"
    Directory = Join-Path $script:RepoRoot "apps\api"
    File = "docker-compose.yml"
    Build = $true
  },
  @{
    Name = "HomePit Web"
    Project = "homepit-web"
    Directory = Join-Path $script:RepoRoot "apps\web"
    File = "docker-compose.yml"
    Build = $true
  }
)

function Test-Executable {
  param([string]$Name)
  return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Test-CommandSucceeds {
  param(
    [string]$Command,
    [string[]]$Arguments
  )

  try {
    & $Command @Arguments *> $null
    return $LASTEXITCODE -eq 0
  }
  catch {
    return $false
  }
}

function New-RuntimeCandidate {
  param([ValidateSet("docker", "podman")] [string]$Name)

  if ($Name -eq "docker") {
    if (-not (Test-Executable "docker")) {
      return $null
    }

    if (-not (Test-CommandSucceeds "docker" @("info"))) {
      return $null
    }

    if (Test-CommandSucceeds "docker" @("compose", "version")) {
      return [pscustomobject]@{
        Name = "Docker"
        EngineCommand = "docker"
        ComposeCommand = @("docker", "compose")
      }
    }

    return $null
  }

  if (-not (Test-Executable "podman")) {
    return $null
  }

  if (-not (Test-CommandSucceeds "podman" @("info"))) {
    return $null
  }

  if (Test-CommandSucceeds "podman" @("compose", "version")) {
    return [pscustomobject]@{
      Name = "Podman"
      EngineCommand = "podman"
      ComposeCommand = @("podman", "compose")
    }
  }

  if ((Test-Executable "podman-compose") -and (Test-CommandSucceeds "podman-compose" @("version"))) {
    return [pscustomobject]@{
      Name = "Podman"
      EngineCommand = "podman"
      ComposeCommand = @("podman-compose")
    }
  }

  return $null
}

function Resolve-ContainerRuntime {
  param([ValidateSet("auto", "docker", "podman")] [string]$PreferredEngine)

  $candidates = if ($PreferredEngine -eq "auto") {
    @("docker", "podman")
  }
  else {
    @($PreferredEngine)
  }

  foreach ($candidate in $candidates) {
    $runtime = New-RuntimeCandidate -Name $candidate
    if ($null -ne $runtime) {
      return $runtime
    }
  }

  throw "Nao encontrei Docker Compose nem Podman Compose em execucao. Abra o Docker Desktop ou inicie o Podman e tente novamente."
}

function Invoke-Compose {
  param(
    [hashtable]$Target,
    [string[]]$Arguments
  )

  $command = $script:Runtime.ComposeCommand[0]
  $commandArgs = @()
  if ($script:Runtime.ComposeCommand.Count -gt 1) {
    $commandArgs += $script:Runtime.ComposeCommand[1..($script:Runtime.ComposeCommand.Count - 1)]
  }

  $commandArgs += @("-p", $Target.Project, "-f", $Target.File)
  $commandArgs += $Arguments

  Write-Host ""
  Write-Host "==> $($Target.Name)"
  Write-Host "$command $($commandArgs -join ' ')"

  Push-Location $Target.Directory
  try {
    & $command @commandArgs
    if ($LASTEXITCODE -ne 0) {
      throw "Falha ao executar compose para $($Target.Name)."
    }
  }
  finally {
    Pop-Location
  }
}

function Invoke-Engine {
  param([string[]]$Arguments)

  & $script:Runtime.EngineCommand @Arguments
  return $LASTEXITCODE
}

function Test-NetworkExists {
  Invoke-Engine @("network", "inspect", $script:NetworkName) *> $null
  return $LASTEXITCODE -eq 0
}

function Ensure-Network {
  if (Test-NetworkExists) {
    Write-Host "Rede $script:NetworkName ja existe."
    return
  }

  Write-Host "Criando rede $script:NetworkName..."
  Invoke-Engine @("network", "create", $script:NetworkName) *> $null
  if ($LASTEXITCODE -ne 0) {
    throw "Nao foi possivel criar a rede $script:NetworkName."
  }
}

function Get-EnvValue {
  param(
    [string]$Path,
    [string]$Name
  )

  if (-not (Test-Path -LiteralPath $Path)) {
    return $null
  }

  $escapedName = [regex]::Escape($Name)
  foreach ($line in Get-Content -LiteralPath $Path) {
    if ($line -match "^$escapedName=(.*)$") {
      return $Matches[1]
    }
  }

  return $null
}

function New-LocalSecret {
  param([int]$ByteCount = 32)

  $bytes = [byte[]]::new($ByteCount)
  [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
  return (($bytes | ForEach-Object { $_.ToString("x2") }) -join "")
}

function Ensure-EnvFile {
  param(
    [string]$Path,
    [string[]]$Lines
  )

  if (Test-Path -LiteralPath $Path) {
    Write-Host "Mantendo ambiente existente: $Path"
    return
  }

  $directory = Split-Path -Parent $Path
  if (-not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
  }

  Set-Content -LiteralPath $Path -Value $Lines -Encoding utf8
  Write-Host "Criado ambiente local: $Path"
}

function Ensure-LocalEnvironment {
  $supabaseEnv = Join-Path $script:RepoRoot "infra\supabase\.env"
  $evolutionEnv = Join-Path $script:RepoRoot "infra\evolution\.env"
  $minioEnv = Join-Path $script:RepoRoot "infra\minio\.env"
  $apiEnv = Join-Path $script:RepoRoot "apps\api\.env"
  $webEnv = Join-Path $script:RepoRoot "apps\web\.env"

  $postgresPassword = Get-EnvValue -Path $supabaseEnv -Name "POSTGRES_PASSWORD"
  if ([string]::IsNullOrWhiteSpace($postgresPassword)) {
    $postgresPassword = "homepit_" + (New-LocalSecret 24)
  }

  $evolutionApiKey = Get-EnvValue -Path $evolutionEnv -Name "EVOLUTION_API_KEY"
  if ([string]::IsNullOrWhiteSpace($evolutionApiKey)) {
    $evolutionApiKey = "homepit_" + (New-LocalSecret 24)
  }

  $evolutionDbPassword = Get-EnvValue -Path $evolutionEnv -Name "EVOLUTION_DB_PASSWORD"
  if ([string]::IsNullOrWhiteSpace($evolutionDbPassword)) {
    $evolutionDbPassword = "homepit_" + (New-LocalSecret 24)
  }

  $jwtSigningKey = Get-EnvValue -Path $apiEnv -Name "Jwt__SigningKey"
  if ([string]::IsNullOrWhiteSpace($jwtSigningKey)) {
    $jwtSigningKey = "homepit-local-" + (New-LocalSecret 32)
  }

  $minioRootUser = Get-EnvValue -Path $minioEnv -Name "MINIO_ROOT_USER"
  if ([string]::IsNullOrWhiteSpace($minioRootUser)) {
    $minioRootUser = "homepitminio"
  }

  $minioRootPassword = Get-EnvValue -Path $minioEnv -Name "MINIO_ROOT_PASSWORD"
  if ([string]::IsNullOrWhiteSpace($minioRootPassword)) {
    $minioRootPassword = "homepit_" + (New-LocalSecret 24)
  }

  Ensure-EnvFile -Path $supabaseEnv -Lines @(
    "POSTGRES_PASSWORD=$postgresPassword",
    "POSTGRES_USER=supabase_admin",
    "POSTGRES_DB=postgres",
    "POSTGRES_PORT=54322",
    "STUDIO_PORT=54323",
    "PG_META_PORT=54324"
  )

  Ensure-EnvFile -Path $evolutionEnv -Lines @(
    "EVOLUTION_PORT=8081",
    "EVOLUTION_API_KEY=$evolutionApiKey",
    "EVOLUTION_SERVER_URL=http://localhost:8081",
    "EVOLUTION_DB_PASSWORD=$evolutionDbPassword",
    "EVOLUTION_DB_NAME=evolution",
    "EVOLUTION_DB_USER=evolution"
  )

  Ensure-EnvFile -Path $minioEnv -Lines @(
    "MINIO_IMAGE=minio/minio:latest",
    "MINIO_ROOT_USER=$minioRootUser",
    "MINIO_ROOT_PASSWORD=$minioRootPassword",
    "MINIO_PORT=9000",
    "MINIO_CONSOLE_PORT=9001"
  )

  Ensure-EnvFile -Path $apiEnv -Lines @(
    "ASPNETCORE_ENVIRONMENT=Development",
    "ASPNETCORE_URLS=http://+:8080",
    "ConnectionStrings__HomePitDb=Host=supabase-db;Port=5432;Database=postgres;Username=supabase_admin;Password=$postgresPassword",
    "Database__ApplyMigrationsOnStartup=true",
    "Jwt__Issuer=homepit",
    "Jwt__Audience=homepit",
    "Jwt__SigningKey=$jwtSigningKey",
    "Jwt__AccessTokenMinutes=30",
    "EvolutionApi__BaseUrl=http://evolution-api:8080",
    "EvolutionApi__InstanceName=homepit",
    "EvolutionApi__ApiKey=$evolutionApiKey",
    "EvolutionApi__SendTextPathTemplate=/message/sendText/{instance}",
    "ObjectStorage__Endpoint=http://homepit-minio:9000",
    "ObjectStorage__AccessKey=$minioRootUser",
    "ObjectStorage__SecretKey=$minioRootPassword",
    "ObjectStorage__BucketName=homepit-assets",
    "ObjectStorage__UseSsl=false",
    "ObjectStorage__CreateBucketOnStartup=true",
    "Notifications__DailyDigestEnabled=false",
    "Notifications__PollIntervalMinutes=5",
    "Cors__AllowedOrigins__0=http://localhost:3000"
  )

  Ensure-EnvFile -Path $webEnv -Lines @(
    "NEXT_PUBLIC_API_BASE_URL=http://localhost:8080",
    "WEB_PORT=3000"
  )
}

function Select-Action {
  Write-Host "HomePit local"
  Write-Host "1) Iniciar aplicacao"
  Write-Host "2) Parar aplicacao"
  Write-Host "3) Apagar aplicacao e volumes"
  Write-Host ""

  $choice = Read-Host "Escolha uma opcao"
  switch ($choice) {
    "1" { return "start" }
    "2" { return "stop" }
    "3" { return "destroy" }
    default { throw "Opcao invalida: $choice" }
  }
}

function Confirm-Destroy {
  if ($Yes) {
    return
  }

  Write-Host ""
  Write-Host "Esta acao remove containers e volumes locais do HomePit."
  Write-Host "Os dados dos bancos locais serao apagados."
  $answer = Read-Host "Digite APAGAR para confirmar"
  if ($answer -ne "APAGAR") {
    throw "Operacao cancelada."
  }
}

function Start-HomePit {
  Ensure-LocalEnvironment
  Ensure-Network

  foreach ($target in $script:ComposeTargets) {
    $args = @("up", "-d")
    if ((-not $NoBuild) -and $target.Build) {
      $args += "--build"
    }

    Invoke-Compose -Target $target -Arguments $args
  }

  Write-Host ""
  Write-Host "HomePit local iniciado."
  Write-Host "Web:              http://localhost:3000"
  Write-Host "API healthcheck:  http://localhost:8080/health"
  Write-Host "Supabase Studio:  http://localhost:54323"
  Write-Host "Evolution API:    http://localhost:8081"
  Write-Host "MinIO API:        http://localhost:9000"
  Write-Host "MinIO Console:    http://localhost:9001"
}

function Stop-HomePit {
  $targets = @($script:ComposeTargets)
  [array]::Reverse($targets)

  foreach ($target in $targets) {
    Invoke-Compose -Target $target -Arguments @("down", "--remove-orphans")
  }

  Write-Host ""
  Write-Host "HomePit local parado. Volumes preservados."
}

function Destroy-HomePit {
  Confirm-Destroy

  $targets = @($script:ComposeTargets)
  [array]::Reverse($targets)

  foreach ($target in $targets) {
    Invoke-Compose -Target $target -Arguments @("down", "--volumes", "--remove-orphans")
  }

  if (Test-NetworkExists) {
    Write-Host ""
    Write-Host "Removendo rede $script:NetworkName..."
    Invoke-Engine @("network", "rm", $script:NetworkName) *> $null
    if ($LASTEXITCODE -ne 0) {
      Write-Warning "Nao foi possivel remover a rede $script:NetworkName. Ela pode estar em uso por outros containers."
    }
  }

  Write-Host ""
  Write-Host "HomePit local removido com volumes."
}

if ([string]::IsNullOrWhiteSpace($Action)) {
  $Action = Select-Action
}

$script:Runtime = Resolve-ContainerRuntime -PreferredEngine $Engine
Write-Host "Usando $($script:Runtime.Name)."

switch ($Action) {
  "start" { Start-HomePit }
  "stop" { Stop-HomePit }
  "destroy" { Destroy-HomePit }
}
