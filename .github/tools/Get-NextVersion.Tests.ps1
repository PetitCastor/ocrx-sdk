# Checks for Get-NextVersion.ps1: how a bump (or an override) becomes the published version. No
# Pester dependency: plain assertions, run directly with
# `pwsh -File .github/tools/Get-NextVersion.Tests.ps1`; ci.yml runs it on every pull request and
# again before every release. Tags are injected through -Tag, so no git state is involved.

$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Get-NextVersion.ps1'
$failures = [System.Collections.Generic.List[string]]::new()

function Assert-Version {
    param([string]$Name, [string]$Expected, [hashtable]$Arguments)
    try { $actual = [string](& $script @Arguments) }
    catch { $failures.Add("$Name : expected [$Expected] but it threw: $($_.Exception.Message)"); return }
    if ($Expected -cne $actual) { $failures.Add("$Name : expected [$Expected] but got [$actual]") }
}

function Assert-Throws {
    param([string]$Name, [string]$Pattern, [hashtable]$Arguments)
    try { & $script @Arguments | Out-Null; $failures.Add("$Name : expected an error, but none was raised.") }
    catch { if ($_.Exception.Message -notmatch $Pattern) { $failures.Add("$Name : error did not match /$Pattern/: $($_.Exception.Message)") } }
}

$tags = @('v2.1.0', 'v2.4.1', 'v2.8.0', 'v2.7.0')

# --- bump --------------------------------------------------------------------------------------
Assert-Version 'patch moves the patch' '2.8.1' @{ Bump = 'patch'; Tag = $tags }
Assert-Version 'minor resets the patch' '2.9.0' @{ Bump = 'minor'; Tag = $tags + 'v2.8.3' }
Assert-Version 'major resets minor and patch' '3.0.0' @{ Bump = 'major'; Tag = $tags }
Assert-Version 'patch is the default bump' '2.8.1' @{ Tag = $tags }
Assert-Version 'the highest tag wins, not the last listed' '2.8.1' @{ Bump = 'patch'; Tag = @('v2.8.0', 'v2.2.0') }
Assert-Version 'versions compare numerically, not as text' '2.10.1' @{ Bump = 'patch'; Tag = @('v2.9.0', 'v2.10.0') }

# --- which tags count --------------------------------------------------------------------------
Assert-Version 'prerelease tags are not a base' '2.8.1' @{ Bump = 'patch'; Tag = $tags + 'v2.9.0-rc.1' }
Assert-Version 'non-version tags are ignored' '2.8.1' @{ Bump = 'patch'; Tag = $tags + @('latest', 'release-2.9.0', 'v2.9') }
Assert-Throws 'no stable tag cannot bump' 'nothing to bump from' @{ Bump = 'patch'; Tag = @('v2.0.0-rc.1') }
Assert-Throws 'no tag at all cannot bump' 'nothing to bump from' @{ Bump = 'minor'; Tag = @() }

# --- override ----------------------------------------------------------------------------------
Assert-Version 'an override above the latest wins' '2.9.5' @{ VersionOverride = '2.9.5'; Tag = $tags }
Assert-Version 'an override beats the bump' '4.0.0' @{ Bump = 'patch'; VersionOverride = '4.0.0'; Tag = $tags }
Assert-Version 'a blank override means use the bump' '2.8.1' @{ Bump = 'patch'; VersionOverride = '  '; Tag = $tags }
Assert-Version 'an override needs no tag to start a train' '2.0.0' @{ VersionOverride = '2.0.0'; Tag = @() }
Assert-Throws 'override equal to the latest' 'strictly greater' @{ VersionOverride = '2.8.0'; Tag = $tags }
Assert-Throws 'override below the latest' 'strictly greater' @{ VersionOverride = '2.4.2'; Tag = $tags }
Assert-Throws 'override numerically below, textually above' 'strictly greater' @{ VersionOverride = '2.9.0'; Tag = @('v2.10.0') }
Assert-Throws 'override with a v prefix' 'strict stable semver' @{ VersionOverride = 'v2.9.0'; Tag = $tags }
Assert-Throws 'override with a prerelease suffix' 'strict stable semver' @{ VersionOverride = '2.9.0-rc.1'; Tag = $tags }
Assert-Throws 'override with two components' 'strict stable semver' @{ VersionOverride = '2.9'; Tag = $tags }
Assert-Throws 'override with a leading zero' 'strict stable semver' @{ VersionOverride = '2.09.0'; Tag = $tags }
Assert-Throws 'override that overflows' 'strict stable semver' @{ VersionOverride = '2.9.99999999999'; Tag = $tags }
Assert-Throws 'override that is not a version' 'strict stable semver' @{ VersionOverride = 'next'; Tag = $tags }

# --- input validation --------------------------------------------------------------------------
Assert-Throws 'unknown bump level' 'patch|minor|major' @{ Bump = 'huge'; Tag = $tags }

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "FAIL $_" }
    Write-Host "Failed: $($failures.Count)"
    exit 1
}
Write-Host 'Failed: 0'
