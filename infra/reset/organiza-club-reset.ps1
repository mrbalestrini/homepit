param(
  [Parameter(Mandatory = $true)]
  [string]$DatabaseHost,

  [string]$DatabasePort = "5432",
  [string]$DatabaseName = "postgres",
  [string]$DatabaseUser = "supabase_admin",
  [string]$StorageEndpoint,

  [switch]$SkipDatabase,
  [switch]$SkipStorage,
  [switch]$Execute,
  [string]$Confirmation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:LegacySchema = "homepit"
$script:NewSchema = "organiza_club"
$script:LegacyBucket = "homepit-assets"
$script:NewBucket = "organiza-club-assets"
$script:ConfirmationText = "RESET-HOMEPIT-PARA-ORGANIZA-CLUB"
$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$script:KnownLegacyMigrations = @(
  "20260527162000_InitialCreate",
  "20260528120000_AddActivityComments",
  "20260529100000_AddPermissionsModel",
  "20260530194500_AddUniverseImageUrl",
  "20260601161000_AddUserProfilePhoto",
  "20260603152000_AddPromptBankModule",
  "20260604173000_AddUniverseUploadedImages",
  "20260610120000_IncreasePromptTextLength",
  "20260615160000_AddInstitutionalPageCms",
  "20260618110000_AddInstitutionalSeoImage",
  "20260620120000_AddActivityDueDate",
  "20260622120000_AddActivityImages",
  "20260623120000_AddGsmNumbers",
  "20260624120000_AddGsmPlanAndMonthlyCost",
  "20260624130000_AddGsmRechargeHistoryAndDaysWithoutRecharge",
  "20260624150000_AddPromptArchiving",
  "20260706120000_AddFinanceModule",
  "20260707110000_AddFinanceCategories",
  "20260709120000_AddAccountLifecycle",
  "20260709153000_AddCommercialPlans",
  "20260710140438_AddPlatformSettings",
  "20260710165742_AddHouseholdCommercialOwnership",
  "20260710192241_AddToolImprovementSuggestions",
  "20260710193000_RefactorPlanTotals",
  "20260713160000_AddMemberEffortAllocations",
  "20260713170000_AddHouseholdInvitations",
  "20260714120000_AddPopularPlanFlag",
  "20260714130000_AddShowInCatalogFlag",
  "20260714150000_AddActivityCompletedAt",
  "20260714190935_AddIntegrationConnections",
  "20260716185107_AddIntegrationOptimisticConcurrency",
  "20260716192112_AddOAuthMcp"
)

function Assert-Executable {
  param([string]$Name)

  if ($null -eq (Get-Command $Name -ErrorAction SilentlyContinue)) {
    throw "O executavel '$Name' nao foi encontrado."
  }
}

function Invoke-PostgresQuery {
  param([string]$Sql)

  $arguments = @(
    "--host", $DatabaseHost,
    "--port", $DatabasePort,
    "--username", $DatabaseUser,
    "--dbname", $DatabaseName,
    "--no-password",
    "--tuples-only",
    "--no-align",
    "--set", "ON_ERROR_STOP=1",
    "--command", $Sql
  )
  $result = & psql @arguments
  if ($LASTEXITCODE -ne 0) {
    throw "A validacao PostgreSQL falhou."
  }

  return @($result | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })
}

function Test-DatabaseTarget {
  Assert-Executable "psql"

  if ([string]::IsNullOrWhiteSpace($DatabaseHost) -or
      [string]::IsNullOrWhiteSpace($DatabaseName) -or
      [string]::IsNullOrWhiteSpace($DatabaseUser)) {
    throw "Host, banco e usuario PostgreSQL devem ser informados explicitamente."
  }

  $identity = @(Invoke-PostgresQuery "SELECT current_database() || '|' || current_user;")
  if ($identity.Count -ne 1 -or $identity[0] -ne "$DatabaseName|$DatabaseUser") {
    throw "O alvo PostgreSQL conectado nao corresponde ao banco e usuario informados."
  }

  $newSchemaExists = @(Invoke-PostgresQuery "SELECT CASE WHEN to_regnamespace('$script:NewSchema') IS NULL THEN 'false' ELSE 'true' END;")
  if ($newSchemaExists[0] -eq "true") {
    throw "O schema '$script:NewSchema' ja existe. O reset nunca o sobrescreve."
  }

  $legacySchemaExists = @(Invoke-PostgresQuery "SELECT CASE WHEN to_regnamespace('$script:LegacySchema') IS NULL THEN 'false' ELSE 'true' END;")
  if ($legacySchemaExists[0] -ne "true") {
    throw "O schema legado '$script:LegacySchema' nao existe no alvo."
  }

  $markerCount = @(Invoke-PostgresQuery @"
SELECT count(*)
FROM information_schema.tables
WHERE table_schema = '$script:LegacySchema'
  AND table_name IN ('households', 'household_members', 'universes', 'projects');
"@)
  if ([int]$markerCount[0] -lt 3) {
    throw "O schema legado nao contem marcadores suficientes para comprovar que pertence ao HomePit."
  }

  $historySchemas = @(Invoke-PostgresQuery @"
SELECT table_schema
FROM information_schema.tables
WHERE table_name = '__EFMigrationsHistory'
ORDER BY table_schema;
"@)

  $unexpectedHistorySchemas = @($historySchemas | Where-Object { $_ -notin @("public", $script:LegacySchema) })
  if ($unexpectedHistorySchemas.Count -gt 0) {
    throw "Foram encontrados historicos EF em schemas inesperados: $($unexpectedHistorySchemas -join ', ')."
  }

  if ($historySchemas -contains "public") {
    $migrationIds = @(Invoke-PostgresQuery 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";')
    if ($migrationIds.Count -eq 0 -or $migrationIds -notcontains "20260527162000_InitialCreate") {
      throw "O historico EF publico nao pode ser comprovado como pertencente ao HomePit."
    }

    $unknownMigrations = @($migrationIds | Where-Object { $_ -notin $script:KnownLegacyMigrations })
    if ($unknownMigrations.Count -gt 0) {
      throw "O historico EF publico possui migrations alheias ou desconhecidas: $($unknownMigrations -join ', ')."
    }
  }

  return [pscustomobject]@{
    HasPublicHistory = $historySchemas -contains "public"
  }
}

function Reset-Database {
  param([bool]$HasPublicHistory)

  $commands = @("DROP SCHEMA `"$script:LegacySchema`" CASCADE;")
  if ($HasPublicHistory) {
    $commands += 'DROP TABLE public."__EFMigrationsHistory";'
  }

  Invoke-PostgresQuery ($commands -join [Environment]::NewLine) | Out-Null

  $previousConnectionString = $env:ConnectionStrings__OrganizaClubDb
  try {
    $password = $env:PGPASSWORD
    if ([string]::IsNullOrWhiteSpace($password)) {
      throw "Defina PGPASSWORD para aplicar a baseline sem expor a senha na linha de comando."
    }

    $env:ConnectionStrings__OrganizaClubDb = "Host=$DatabaseHost;Port=$DatabasePort;Database=$DatabaseName;Username=$DatabaseUser;Password=$password"
    & dotnet ef database update --project "$script:RepoRoot\apps\api\src\OrganizaClub.Infrastructure\OrganizaClub.Infrastructure.csproj" --startup-project "$script:RepoRoot\apps\api\src\OrganizaClub.Api\OrganizaClub.Api.csproj"
    if ($LASTEXITCODE -ne 0) {
      throw "A baseline Organiza Club nao foi aplicada."
    }
  }
  finally {
    $env:ConnectionStrings__OrganizaClubDb = $previousConnectionString
  }
}

function Test-StorageTarget {
  Assert-Executable "mc"
  if ([string]::IsNullOrWhiteSpace($StorageEndpoint)) {
    throw "StorageEndpoint e obrigatorio quando o storage nao for ignorado."
  }
  if ([string]::IsNullOrWhiteSpace($env:ORGANIZA_RESET_STORAGE_ACCESS_KEY) -or
      [string]::IsNullOrWhiteSpace($env:ORGANIZA_RESET_STORAGE_SECRET_KEY)) {
    throw "Defina ORGANIZA_RESET_STORAGE_ACCESS_KEY e ORGANIZA_RESET_STORAGE_SECRET_KEY."
  }

  $temporaryConfig = Join-Path ([System.IO.Path]::GetTempPath()) ("organiza-club-mc-preflight-" + [guid]::NewGuid().ToString("N"))
  New-Item -ItemType Directory -Path $temporaryConfig | Out-Null
  try {
    & mc --config-dir $temporaryConfig alias set organiza-reset $StorageEndpoint $env:ORGANIZA_RESET_STORAGE_ACCESS_KEY $env:ORGANIZA_RESET_STORAGE_SECRET_KEY *> $null
    if ($LASTEXITCODE -ne 0) { throw "Nao foi possivel validar as credenciais do object storage." }

    & mc --config-dir $temporaryConfig stat "organiza-reset/$script:LegacyBucket" *> $null
    if ($LASTEXITCODE -ne 0) { throw "O bucket legado '$script:LegacyBucket' nao existe no alvo." }

    & mc --config-dir $temporaryConfig stat "organiza-reset/$script:NewBucket" *> $null
    if ($LASTEXITCODE -eq 0) { throw "O bucket novo '$script:NewBucket' ja existe. O reset nunca o sobrescreve." }
  }
  finally {
    if (Test-Path -LiteralPath $temporaryConfig) {
      Remove-Item -LiteralPath $temporaryConfig -Recurse -Force
    }
  }
}

function Invoke-StorageReset {
  $temporaryConfig = Join-Path ([System.IO.Path]::GetTempPath()) ("organiza-club-mc-" + [guid]::NewGuid().ToString("N"))
  New-Item -ItemType Directory -Path $temporaryConfig | Out-Null
  try {
    & mc --config-dir $temporaryConfig alias set organiza-reset $StorageEndpoint $env:ORGANIZA_RESET_STORAGE_ACCESS_KEY $env:ORGANIZA_RESET_STORAGE_SECRET_KEY *> $null
    if ($LASTEXITCODE -ne 0) { throw "Nao foi possivel validar as credenciais do object storage." }

    & mc --config-dir $temporaryConfig stat "organiza-reset/$script:LegacyBucket" *> $null
    if ($LASTEXITCODE -ne 0) { throw "O bucket legado '$script:LegacyBucket' nao existe no alvo." }

    & mc --config-dir $temporaryConfig stat "organiza-reset/$script:NewBucket" *> $null
    if ($LASTEXITCODE -eq 0) { throw "O bucket novo '$script:NewBucket' ja existe. O reset nunca o sobrescreve." }

    & mc --config-dir $temporaryConfig rm --recursive --force "organiza-reset/$script:LegacyBucket"
    if ($LASTEXITCODE -ne 0) { throw "Falha ao apagar os objetos do bucket legado." }
    & mc --config-dir $temporaryConfig rb --force "organiza-reset/$script:LegacyBucket"
    if ($LASTEXITCODE -ne 0) { throw "Falha ao remover o bucket legado." }
    & mc --config-dir $temporaryConfig mb "organiza-reset/$script:NewBucket"
    if ($LASTEXITCODE -ne 0) { throw "Falha ao criar o bucket Organiza Club." }
  }
  finally {
    if (Test-Path -LiteralPath $temporaryConfig) {
      Remove-Item -LiteralPath $temporaryConfig -Recurse -Force
    }
  }
}

if ($SkipDatabase -and $SkipStorage) {
  throw "Nao ha nenhum alvo para validar."
}

$databaseTarget = $null
if (-not $SkipDatabase) {
  $databaseTarget = Test-DatabaseTarget
  Write-Host "PostgreSQL validado: $DatabaseHost/$DatabaseName; somente o schema '$script:LegacySchema' sera removido."
}
if (-not $SkipStorage) {
  Test-StorageTarget
  Write-Host "Object storage configurado: somente '$script:LegacyBucket' sera removido e '$script:NewBucket' sera criado."
}

if (-not $Execute) {
  Write-Host "Preflight concluido. Nenhuma alteracao foi executada."
  Write-Host "Para executar, repita com -Execute -Confirmation '$script:ConfirmationText'."
  exit 0
}
if ($Confirmation -ne $script:ConfirmationText) {
  throw "Confirmacao invalida. Use exatamente '$script:ConfirmationText'."
}

if (-not $SkipDatabase) {
  Reset-Database -HasPublicHistory $databaseTarget.HasPublicHistory
}
if (-not $SkipStorage) {
  Invoke-StorageReset
}

Write-Host "Reset concluido. Gere novos JWT, pepper e chaves OAuth antes de iniciar a aplicacao."
