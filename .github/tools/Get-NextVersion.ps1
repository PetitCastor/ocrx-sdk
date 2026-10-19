# Computes the version an SDK release publishes, so the rule is written down once and tested
# (Get-NextVersion.Tests.ps1) instead of living as inline workflow script. Runs on Windows
# PowerShell 5.1 and pwsh. Writes the bare version (no "v") to the pipeline and throws on any
# input it cannot turn into a version that is strictly above the latest stable tag.
#
# A release is an explicit decision: the dispatcher picks a bump, the script derives X.Y.Z from the
# latest stable vX.Y.Z tag.
#
#   -Bump patch|minor|major   next version after the latest stable tag (minor and major reset the
#                             lower components)
#   -VersionOverride X.Y.Z    optional; takes precedence over -Bump. Strict semver (no "v" prefix, no
#                             prerelease or build suffix, no leading zeros) and strictly above the
#                             latest stable tag. For a deliberate jump such as a new major train.
#
# -Tag is the list of existing tags. It defaults to `git tag --list`; the tests inject it. Prerelease
# tags (v2.1.0-rc.1) are ignored when finding the latest: they are rehearsals, not a base to bump.
[CmdletBinding()]
param(
    [ValidateSet('patch', 'minor', 'major')]
    [string]$Bump = 'patch',
    [string]$VersionOverride = '',
    [string[]]$Tag
)

$ErrorActionPreference = 'Stop'

if ($null -eq $Tag) {
    $Tag = @(git tag --list)
    if ($LASTEXITCODE -ne 0) { throw 'git tag --list failed; cannot find the latest stable tag.' }
}

$semver = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$'

function ConvertTo-StableVersion {
    # $null when the text is not strict X.Y.Z or a component overflows [int].
    param([string]$Text)
    if ($Text -notmatch $semver) { return $null }
    $parsed = $null
    if ([version]::TryParse($Text, [ref]$parsed)) { return $parsed }
    return $null
}

$latest = $null
foreach ($name in $Tag) {
    if ($name -cnotmatch '^v(.+)$') { continue }
    $candidate = ConvertTo-StableVersion $Matches[1]
    if ($null -ne $candidate -and ($null -eq $latest -or $candidate -gt $latest)) { $latest = $candidate }
}

$override = $VersionOverride.Trim()
if ($override -ne '') {
    $next = ConvertTo-StableVersion $override
    if ($null -eq $next) { throw "version_override must be strict stable semver X.Y.Z (no 'v' prefix, no suffix); got '$override'." }
    if ($null -ne $latest -and $next -le $latest) {
        throw "version_override $next must be strictly greater than the latest stable tag v$latest."
    }
}
else {
    if ($null -eq $latest) { throw "No stable vX.Y.Z tag exists, so '$Bump' has nothing to bump from; pass version_override." }
    switch ($Bump) {
        'major' { $next = [version]::new($latest.Major + 1, 0, 0) }
        'minor' { $next = [version]::new($latest.Major, $latest.Minor + 1, 0) }
        default { $next = [version]::new($latest.Major, $latest.Minor, $latest.Build + 1) }
    }
}

$next.ToString()
