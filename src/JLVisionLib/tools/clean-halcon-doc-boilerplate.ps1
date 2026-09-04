$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'JLVisionLib.Runtime'

$origSummaryTail = ' \u539f\u59cb\u8bf4\u660e\uff1a[^<]*'
$createInstanceLead = '^(\u521b\u5efa \w+ \u5b9e\u4f8b\u3002)\s*[A-Z]'

function Clean-DocLine([string]$line) {
    if ($line -notmatch '^\t///') { return $line }

    if ($line -match '^\t///\s+(Modified instance represents:|Instance represents:)') {
        return $null
    }

    if ($line -match '^(?<prefix>\t///\s*)(?<body>.*)$') {
        $prefix = $Matches['prefix']
        $body = $Matches['body']

        $body = [regex]::Replace($body, $origSummaryTail, '')
        $body = [regex]::Replace($body, ' Modified instance represents:[^<]*', '')
        $body = [regex]::Replace($body, ' Instance represents:[^<]*', '')

        if ($body -match $createInstanceLead) {
            $body = $Matches[1]
        }

        # Fix unclosed <para> after tail removal
        if ($body -match '<para>(?!.*</para>)') {
            $body = $body.TrimEnd() + '</para>'
        }

        return $prefix + $body.TrimEnd()
    }

    return $line
}

$totalFiles = 0
Get-ChildItem $runtime -Filter '*.cs' | ForEach-Object {
    $lines = [IO.File]::ReadAllLines($_.FullName)
    $out = New-Object System.Collections.Generic.List[string]
    $changed = $false
    foreach ($line in $lines) {
        $cleaned = Clean-DocLine $line
        if ($null -eq $cleaned) { $changed = $true; continue }
        if ($cleaned -ne $line) { $changed = $true }
        $out.Add($cleaned)
    }
    if ($changed) {
        [IO.File]::WriteAllLines($_.FullName, $out, (New-Object System.Text.UTF8Encoding $false))
        $totalFiles++
        Write-Host "Fixed $($_.Name)"
    }
}

Write-Host "Done. Updated $totalFiles file(s)."
