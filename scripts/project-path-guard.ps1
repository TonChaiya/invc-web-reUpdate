# Shared path-containment rule for every release script that reads, writes or deletes under the project.
# Dot-source this file:  . (Join-Path $PSScriptRoot 'project-path-guard.ps1')
#
# Rule (exact boundary, not a string prefix):
#   normalized(project root) + '\'  must be a prefix of  normalized(candidate)      -> C:\INVC\Web\anything  ACCEPT
#   sibling names that merely start with the root are rejected                      -> C:\INVC\Web2, C:\INVC\Web-outside  REJECT
#   parent traversal is resolved before the check                                    -> C:\INVC\Web\..\x  REJECT
# Reparse points (junctions / symlinks) in the EXISTING part of the candidate chain below the root are rejected, so a
# write or delete can never be redirected outside the project. Comparison is OrdinalIgnoreCase (Windows paths).

function Get-NormalizedProjectPath {
    param([Parameter(Mandatory)][string]$Path)
    # GetFullPath resolves '..' and '.' without requiring the path to exist; trim trailing separators.
    return [System.IO.Path]::GetFullPath($Path).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
}

function Test-ReparsePointInChain {
    # True when any EXISTING component strictly below the project root (up to and including the candidate) is a reparse point.
    param([Parameter(Mandatory)][string]$NormalizedCandidate, [Parameter(Mandatory)][string]$NormalizedRoot)
    $current = $NormalizedCandidate
    while ($current -and $current.Length -gt $NormalizedRoot.Length) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force -ErrorAction SilentlyContinue
            if ($item -and ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) { return $true }
        }
        $parent = [System.IO.Path]::GetDirectoryName($current)
        if (-not $parent -or $parent -eq $current) { break }
        $current = $parent
    }
    return $false
}

function Test-PathInsideProject {
    <#
      .SYNOPSIS  True when $Candidate is the project root or a descendant of it, with no reparse point in the chain.
      .PARAMETER RequireDescendant  Also reject the project root itself (the candidate must be strictly below the root).
    #>
    param(
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$ProjectRoot,
        [switch]$RequireDescendant
    )
    try {
        $root = Get-NormalizedProjectPath $ProjectRoot
        $full = Get-NormalizedProjectPath $Candidate
    } catch { return $false }

    $isRoot = [string]::Equals($full, $root, [System.StringComparison]::OrdinalIgnoreCase)
    $prefix = $root + [System.IO.Path]::DirectorySeparatorChar
    $isDescendant = $full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)

    if ($RequireDescendant) { if (-not $isDescendant) { return $false } }
    elseif (-not ($isRoot -or $isDescendant)) { return $false }

    if ($isDescendant -and (Test-ReparsePointInChain -NormalizedCandidate $full -NormalizedRoot $root)) {
        Write-Verbose "reparse point detected in path chain: $full"
        return $false
    }
    return $true
}

function Assert-PathInsideProject {
    <# Writes a REFUSED line and exits 2 when the candidate is outside the project (or crosses a reparse point). #>
    param(
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$ProjectRoot,
        [string]$Purpose = "path",
        [switch]$RequireDescendant
    )
    if (-not (Test-PathInsideProject -Candidate $Candidate -ProjectRoot $ProjectRoot -RequireDescendant:$RequireDescendant)) {
        $why = "outside the project root '$ProjectRoot' (exact-boundary check)"
        try {
            $root = Get-NormalizedProjectPath $ProjectRoot; $full = Get-NormalizedProjectPath $Candidate
            if ($full.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and (Test-ReparsePointInChain -NormalizedCandidate $full -NormalizedRoot $root)) {
                $why = "crosses a reparse point / junction below the project root (refused: write or delete could be redirected)"
            } elseif ($RequireDescendant -and [string]::Equals($full, $root, [System.StringComparison]::OrdinalIgnoreCase)) {
                $why = "is the project root itself; a descendant path is required"
            }
        } catch { }
        Write-Host "REFUSED: $Purpose '$Candidate' $why. Release tooling only operates inside the project." -ForegroundColor Red
        exit 2
    }
}
