$ErrorActionPreference = 'Stop'
$root = 'E:\JLVisionLib\JLVisionLib'

function Fix-NativeInteropFile([string]$path) {
    $text = [IO.File]::ReadAllText($path)
    $original = $text

    $text = $text.Replace('EntryPoint = "JlLI', 'EntryPoint = "HLI')
    $text = $text.Replace('EntryPoint = "JlGetMemoryAllocatorType"', 'EntryPoint = "HGetMemoryAllocatorType"')
    $text = $text.Replace('EntryPoint = "JlSetMemoryAllocatorType"', 'EntryPoint = "HSetMemoryAllocatorType"')
    $text = [regex]::Replace($text, '\bJlLI', 'HLI')
    $text = [regex]::Replace($text, '\bJlX(Create|Clear|Exit|Start|Prepare|Join|Thread)', 'HX$1')
    $text = [regex]::Replace($text, '\bJlWindowStack', 'HWindowStack')

    if ($text -ne $original) {
        [IO.File]::WriteAllText($path, $text)
        Write-Host "Fixed $path"
    }
}

@(
    Join-Path $root 'JlNativeApi.cs'
    Join-Path $root 'JlDevThread.cs'
    Join-Path $root 'JlDevThreadContext.cs'
    Join-Path $root 'JlDevParamGuard.cs'
    Join-Path $root 'JlDevWindowStack.cs'
) | ForEach-Object { Fix-NativeInteropFile $_ }

$procIds = Join-Path $root 'JlProcIDs.cs'
$procText = [IO.File]::ReadAllText($procIds)
$procReplacements = [ordered]@{
    'JlOM_MAT' = 'JL_HOM_MAT'
    'JlOM_VECTOR' = 'JL_HOM_VECTOR'
    'JlYSTERESIS' = 'JL_YSTERESIS'
    'JlISTO' = 'JL_HISTO'
    'JlAMMING' = 'JL_HAMMING'
    'JlIT_OR_MISS' = 'JL_HIT_OR_MISS'
    'JlOUGH' = 'JL_HOUGH'
    'JlARMONIC' = 'JL_HARMONIC'
    'JlIGHPASS' = 'JL_HIGHPASS'
    'JlAND_EYE' = 'JL_HAND_EYE'
    'JlANDLE' = 'JL_HANDLE'
    'JlEIGHT_WIDTH' = 'JL_HEIGHT_WIDTH'
}
foreach ($pair in $procReplacements.GetEnumerator()) {
    $procText = $procText.Replace($pair.Key, $pair.Value)
}
[IO.File]::WriteAllText($procIds, $procText)
Write-Host 'Fixed JlProcIDs.cs'
