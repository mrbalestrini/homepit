[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$notes = [System.Collections.Generic.List[string]]::new()

function Add-Failure {
  param([string]$Message)
  $script:failures.Add($Message)
}

function Add-WarningNote {
  param([string]$Message)
  $script:warnings.Add($Message)
}

function Add-Note {
  param([string]$Message)
  $script:notes.Add($Message)
}

function Require-Path {
  param(
    [string]$Path,
    [string]$Label
  )

  if (-not (Test-Path $Path)) {
    Add-Failure "$Label ausente: $Path"
    return $false
  }

  return $true
}

function Get-MetadataValue {
  param(
    [string]$Path,
    [string]$Key
  )

  if (-not (Test-Path $Path)) {
    return $null
  }

  $escapedKey = [regex]::Escape($Key)
  $match = Select-String -Path $Path -Pattern "^\s*${escapedKey}:\s*(.+?)\s*$" | Select-Object -First 1
  if ($null -eq $match) {
    return $null
  }

  return $match.Matches[0].Groups[1].Value.Trim()
}

function Get-LatestChangelogVersion {
  param([string]$Path)

  $match = Select-String -Path $Path -Pattern '^## \[(.+?)\] - ' | Select-Object -First 1
  if ($null -eq $match) {
    return $null
  }

  return $match.Matches[0].Groups[1].Value.Trim()
}

function Test-AllTasksCompleted {
  param([string]$Path)

  if (-not (Test-Path $Path)) {
    return $false
  }

  $taskLines = Select-String -Path $Path -Pattern '^- \[( |x)\] '
  if ($taskLines.Count -eq 0) {
    return $false
  }

  return -not (Select-String -Path $Path -Pattern '^- \[ \] ' | Select-Object -First 1)
}

function Assert-Match {
  param(
    [string]$Left,
    [string]$Right,
    [string]$Label
  )

  if ($Left -ne $Right) {
    Add-Failure "$Label divergente: '$Left' vs '$Right'"
  }
}

$requiredPaths = @(
  "AGENTS.md",
  ".specs\README.md",
  ".specs\active-change.md",
  ".specs\shared\sources-of-truth.md",
  ".specs\templates\feature.md",
  ".specs\templates\bugfix.md",
  ".specs\templates\refactor.md",
  ".specs\templates\database-change.md",
  ".specs\templates\pr-review.md",
  "apps\web\package.json",
  "apps\web\package-lock.json",
  "CHANGELOG.md",
  "contracts\openapi\organiza-club.v1.yaml",
  "apps\api\src\OrganizaClub.Api\Program.cs",
  "infra\setup\organiza-club-local.ps1",
  "apps\api\docker-compose.yml"
)

foreach ($path in $requiredPaths) {
  [void](Require-Path -Path $path -Label "Arquivo obrigatorio")
}

$activeStatus = Get-MetadataValue -Path ".specs\active-change.md" -Key "status"
$activeChange = Get-MetadataValue -Path ".specs\active-change.md" -Key "change"

if ([string]::IsNullOrWhiteSpace($activeStatus)) {
  Add-Failure "`.specs/active-change.md` precisa declarar `status`."
}

if ($activeStatus -eq "active") {
  if ([string]::IsNullOrWhiteSpace($activeChange)) {
    Add-Failure "Mudanca ativa sem campo `change` em `.specs/active-change.md`."
  }
  elseif (-not (Test-Path (Join-Path ".specs\changes" $activeChange))) {
    Add-Failure "Mudanca ativa declarada nao encontrada em `.specs/changes`: $activeChange"
  }
}
elseif ($activeStatus -ne "none" -and $activeStatus -ne "paused") {
  Add-Failure "Status invalido em `.specs/active-change.md`: $activeStatus"
}

$changeDirectories = @(Get-ChildItem ".specs\changes" -Directory -ErrorAction SilentlyContinue)
foreach ($changeDirectory in $changeDirectories) {
  $specPath = Join-Path $changeDirectory.FullName "spec.md"
  $decisionsPath = Join-Path $changeDirectory.FullName "decisions.md"
  $tasksPath = Join-Path $changeDirectory.FullName "tasks.md"

  [void](Require-Path -Path $specPath -Label "Spec da mudanca")
  [void](Require-Path -Path $decisionsPath -Label "Decisoes da mudanca")
  [void](Require-Path -Path $tasksPath -Label "Tarefas da mudanca")

  $isActiveDirectory = $activeStatus -eq "active" -and $changeDirectory.Name -eq $activeChange
  if (-not $isActiveDirectory -and (Test-AllTasksCompleted -Path $tasksPath)) {
    Add-Failure "Mudanca concluida ainda permanece em `.specs/changes`: $($changeDirectory.Name)"
  }
}

$packageJson = Get-Content -Raw "apps\web\package.json" | ConvertFrom-Json
$packageLockVersionMatch = Select-String -Path "apps\web\package-lock.json" -Pattern '^\s*"version":\s*"(.+?)"' | Select-Object -First 1
$latestChangelogVersion = Get-LatestChangelogVersion -Path "CHANGELOG.md"

if ($null -eq $packageLockVersionMatch) {
  Add-Failure "Nao foi possivel identificar a versao principal em package-lock.json."
}
else {
  $packageLockVersion = $packageLockVersionMatch.Matches[0].Groups[1].Value.Trim()
  Assert-Match -Left $packageJson.version -Right $packageLockVersion -Label "Versao do produto entre package.json e package-lock.json"
}

if ([string]::IsNullOrWhiteSpace($latestChangelogVersion)) {
  Add-Failure "Nao foi possivel identificar a ultima versao em CHANGELOG.md."
}
else {
  Assert-Match -Left $packageJson.version -Right $latestChangelogVersion -Label "Versao do produto entre package.json e CHANGELOG.md"
}

$openApiVersionMatch = Select-String -Path "contracts\openapi\organiza-club.v1.yaml" -Pattern '^\s*version:\s*(.+?)\s*$' | Select-Object -First 1
$systemInfoVersionMatch = Select-String -Path "apps\api\src\OrganizaClub.Api\Program.cs" -Pattern 'version\s*=\s*"(.+?)"' | Select-Object -First 1

if ($null -eq $openApiVersionMatch) {
  Add-Failure "Nao foi possivel ler `info.version` no OpenAPI."
}

if ($null -eq $systemInfoVersionMatch) {
  Add-Failure "Nao foi possivel ler a versao exposta por `/api/system/info`."
}

if ($null -ne $openApiVersionMatch -and $null -ne $systemInfoVersionMatch) {
  $openApiVersion = $openApiVersionMatch.Matches[0].Groups[1].Value.Trim()
  $systemInfoVersion = $systemInfoVersionMatch.Matches[0].Groups[1].Value.Trim()
  Assert-Match -Left $openApiVersion -Right $systemInfoVersion -Label "Versao do contrato/API entre OpenAPI e /api/system/info"
}

$setupApiBaseUrlMatch = Select-String -Path "infra\setup\organiza-club-local.ps1" -Pattern 'NEXT_PUBLIC_API_BASE_URL=http://localhost:(\d+)' | Select-Object -First 1
$setupHealthcheckMatch = Select-String -Path "infra\setup\organiza-club-local.ps1" -Pattern 'http://localhost:(\d+)/health' | Select-Object -First 1
$composeApiPortMatch = Select-String -Path "apps\api\docker-compose.yml" -Pattern '\$\{API_PORT:-(\d+)\}:8080' | Select-Object -First 1
$setupWritesApiPort = Select-String -Path "infra\setup\organiza-club-local.ps1" -Pattern 'API_PORT=' | Select-Object -First 1

if ($null -ne $setupApiBaseUrlMatch -and $null -ne $setupHealthcheckMatch) {
  $setupBasePort = $setupApiBaseUrlMatch.Matches[0].Groups[1].Value
  $setupHealthPort = $setupHealthcheckMatch.Matches[0].Groups[1].Value
  if ($setupBasePort -ne $setupHealthPort) {
    Add-Failure "Setup local usa portas diferentes entre NEXT_PUBLIC_API_BASE_URL e healthcheck: $setupBasePort vs $setupHealthPort"
  }

  if ($null -ne $composeApiPortMatch) {
    $composeDefaultPort = $composeApiPortMatch.Matches[0].Groups[1].Value
    if ($composeDefaultPort -ne $setupBasePort -and $null -eq $setupWritesApiPort) {
      Add-WarningNote "Compose da API publica $composeDefaultPort por padrao, enquanto o setup local espera $setupBasePort e nao grava API_PORT."
    }
  }
}
else {
  Add-WarningNote "Nao foi possivel validar integralmente a porta local entre setup e compose."
}

$agentsPaths = @(Get-ChildItem ".agents\skills" -Directory -ErrorAction SilentlyContinue)
foreach ($skillDirectory in $agentsPaths) {
  $skillMdPath = Join-Path $skillDirectory.FullName "SKILL.md"
  $skillAgentPath = Join-Path $skillDirectory.FullName "agents\openai.yaml"

  [void](Require-Path -Path $skillMdPath -Label "Skill")
  [void](Require-Path -Path $skillAgentPath -Label "Descritor de agente")

  if (Test-Path $skillMdPath) {
    $firstLine = Get-Content $skillMdPath -TotalCount 1
    if ($firstLine -ne "---") {
      Add-Failure "Skill sem frontmatter inicial padrao: $($skillDirectory.Name)"
    }
  }
}

$agentsMentionsActive = Select-String -Path "AGENTS.md" -Pattern 'active-change\.md' | Select-Object -First 1
$agentsMentionsSources = Select-String -Path "AGENTS.md" -Pattern 'sources-of-truth\.md' | Select-Object -First 1

if ($null -eq $agentsMentionsActive) {
  Add-Failure "AGENTS.md nao referencia `.specs/active-change.md`."
}

if ($null -eq $agentsMentionsSources) {
  Add-Failure "AGENTS.md nao referencia `.specs/shared/sources-of-truth.md`."
}

$activeChangeNote = "Mudanca ativa: $activeStatus"
if (-not [string]::IsNullOrWhiteSpace($activeChange)) {
  $activeChangeNote += " ($activeChange)"
}

Add-Note $activeChangeNote
Add-Note "Versao do produto: $($packageJson.version)"

if ($failures.Count -gt 0) {
  Write-Host "FAILURES:" -ForegroundColor Red
  foreach ($failure in $failures) {
    Write-Host " - $failure" -ForegroundColor Red
  }
}

if ($warnings.Count -gt 0) {
  Write-Host "WARNINGS:" -ForegroundColor Yellow
  foreach ($warning in $warnings) {
    Write-Host " - $warning" -ForegroundColor Yellow
  }
}

if ($notes.Count -gt 0) {
  Write-Host "INFO:" -ForegroundColor Cyan
  foreach ($note in $notes) {
    Write-Host " - $note" -ForegroundColor Cyan
  }
}

if ($failures.Count -gt 0) {
  exit 1
}

Write-Host "Workflow de IA validado sem falhas." -ForegroundColor Green
