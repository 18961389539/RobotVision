$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'JLVisionLib.Runtime'

$deleteFiles = @(
    'JlWindow.cs', 'JlDrawingObject.cs', 'JlDevWindowStack.cs',
    'JlLineStyleWPF.cs', 'JlLineStyleWPFConverter.cs',
    'JlInitWindowEventHandler.cs', 'JlInitWindowEventHandlerWPF.cs',
    'JlWInitEventHandler.cs', 'WM.cs', 'MK.cs'
)
foreach ($name in $deleteFiles) {
    $path = Join-Path $runtime $name
    if (Test-Path $path) { Remove-Item $path -Force; Write-Host "Deleted $name" }
}

function Test-RemoveMethod([string]$signature, [string]$fileName) {
    if ($signature -match 'JlWindow|JlDrawingObject|JlDevWindowStack|JlLineStyleWPF|JlInitWindowEventHandler|JlWInitEventHandler') { return $true }
    if ($signature -match 'windowHandle|WindowHandle|fatherWindow|drawHandle|\bdrawID\b|OSWindowHandle|OSDisplayHandle|WINHDC|WINHWnd|windowHandleSource|windowHandleDestination') { return $true }
    if ($signature -match 'QueryWindowType|GetComprise\(') { return $true }
    if ($fileName -eq 'JlOperatorSet.cs') {
        if ($signature -match 'public static void (SetWindowType|GetWindowAttr|SetWindowAttr|QueryWindowType|CreateDrawingObject|ClearDrawingObject|SetDrawingObject|GetDrawingObject|GetDrawingObjectIconic|SetDrawingObjectXld|SetContentUpdateCallback)') { return $true }
        if ($signature -match 'public static void Disp(?!arity)') { return $true }
        if ($signature -match 'public static void DisplayScene3d') { return $true }
        if ($signature -match 'public static void Draw') { return $true }
    }
    return $false
}

function Remove-DisplayMethods([string]$path) {
    $fileName = Split-Path $path -Leaf
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.AddRange([IO.File]::ReadAllLines($path))
    $output = [System.Collections.Generic.List[string]]::new()
    $i = 0
    while ($i -lt $lines.Count) {
        $line = $lines[$i]
        if ($line -match '^\t(public|internal|private|protected) ') {
            $start = $i
            while ($start -gt 0 -and ($lines[$start - 1] -match '^\t///' -or $lines[$start - 1] -match '^\t\[')) { $start-- }
            $j = $i
            $sig = $line
            while ($j -lt $lines.Count -and $lines[$j] -notmatch '\{') {
                if ($j -ne $i) { $sig += ' ' + $lines[$j].Trim() }
                $j++
            }
            if ($j -ge $lines.Count) {
                $output.Add($line)
                $i++
                continue
            }
            $depth = 0
            $k = $j
            do {
                $depth += ([regex]::Matches($lines[$k], '\{')).Count
                $depth -= ([regex]::Matches($lines[$k], '\}')).Count
                $k++
            } while ($k -lt $lines.Count -and $depth -gt 0)
            if (Test-RemoveMethod $sig $fileName) {
                $i = $k
                continue
            }
        }
        $output.Add($line)
        $i++
    }
    if ($output.Count -ne $lines.Count) {
        [IO.File]::WriteAllLines($path, $output)
        Write-Host "Stripped methods from $fileName"
    }
}

Get-ChildItem $runtime -Filter '*.cs' | Where-Object { $_.Name -ne 'JlNativeApi.cs' } | ForEach-Object { Remove-DisplayMethods $_.FullName }

function Remove-JlNativeApiDisplayApi([string]$path) {
    $text = [IO.File]::ReadAllText($path)
    $text = [regex]::Replace($text, '(?ms)\r?\n\t\[EditorBrowsable\(EditorBrowsableState\.Never\)\]\r?\n\tpublic delegate int ContentUpdateCallback\(IntPtr context\);\r?\n', "`r`n")
    $text = [regex]::Replace($text, '(?ms)\r?\n\t\[DllImport\(NativeLib, CallingConvention = NativeCall, EntryPoint = "HLICancelDraw"\)\]\r?\n\tpublic static extern void CancelDraw\(\);\r?\n', "`r`n")
    foreach ($method in @(
        'HWindowStackPush\(IntPtr win_handle\)',
        'HWindowStackPop\(\)',
        'HWindowStackGetActive\(out IntPtr win_handle\)',
        'HWindowStackSetActive\(IntPtr win_handle\)',
        'HWindowStackIsOpen\(\[MarshalAs\(UnmanagedType\.Bool\)\] out bool is_open\)',
        'HWindowStackCloseAll\(\)'
    )) {
        $text = [regex]::Replace($text, "(?ms)\r?\n\t\[DllImport\(NativeLib, CallingConvention = NativeCall\)\]\r?\n\tinternal static extern int $method;\r?\n", "`r`n")
    }
    [IO.File]::WriteAllText($path, $text)
    Write-Host 'Updated JlNativeApi.cs (display APIs only)'
}

Remove-JlNativeApiDisplayApi (Join-Path $runtime 'JlNativeApi.cs')

Write-Host 'Done.'
