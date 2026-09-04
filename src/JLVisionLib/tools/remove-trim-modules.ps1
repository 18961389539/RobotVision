$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'JLVisionLib.Runtime'

function Migrate-SerializationFile([string]$path) {
    if ($path -match 'JlSerializedItem\.cs$|JlSerializationBuffer\.cs$') { return }
    $text = [IO.File]::ReadAllText($path)
    if ($text -notmatch 'JlSerializedItem') { return }

    $text = $text.Replace('JlSerializedItem', 'JlSerializationBuffer')
    $text = [regex]::Replace($text, 'public byte\[\] Serialize', 'public byte[] Serialize')
    $text = [regex]::Replace($text, 'public JlSerializationBuffer Serialize', 'public byte[] Serialize')
    $text = [regex]::Replace($text, 'public void Deserialize(\w+)\(JlSerializationBuffer serializedItemHandle\)', 'public void Deserialize$1(byte[] serializedItemHandle)')
    $text = [regex]::Replace($text, 'public void Deserialize(\w+)\(JlSerializationBuffer serializedItem\)', 'public void Deserialize$1(byte[] serializedItem)')
    $text = [regex]::Replace($text, '(?ms)\t\tJlSerializationBuffer \w+ = (Serialize\w+)\(\);\r?\n\t\tbyte\[\] value = \w+;\r?\n\t\t\w+\.Dispose\(\);', "`t`tbyte[] value = `$1();")
    $text = [regex]::Replace($text, '(?ms)\t\tJlSerializationBuffer \w+ = new JlSerializationBuffer\(\(byte\[\]\)info\.GetValue\("data", typeof\(byte\[\]\)\)\);\r?\n\t\t(\w+)\(\w+\);\r?\n\t\t\w+\.Dispose\(\);', "`t`t`$1((byte[])info.GetValue(`"data`", typeof(byte[])));")
    $text = [regex]::Replace($text, '(?ms)\t\tJlSerializationBuffer \w+ = (Serialize\w+)\(\);\r?\n\t\t\w+\.Serialize\(stream\);\r?\n\t\t\w+\.Dispose\(\);', "`t`tJlSerializationBuffer.WriteToStream(`$1(), stream);")
    $text = [regex]::Replace($text, '(?ms)\t\tJlSerializationBuffer \w+ = JlSerializationBuffer\.Deserialize\(stream\);\r?\n\t\t(\w+)\.(\w+)\(\w+\);\r?\n\t\t\w+\.Dispose\(\);', "`t`t`$1.`$2(JlSerializationBuffer.ReadFromStream(stream));")
    $text = [regex]::Replace($text, '(?ms)\t\tJlSerializationBuffer \w+ = (Serialize\w+)\(\);\r?\n\t\t(\w+) \w+ = new \2\(\);\r?\n\t\t\w+\.(Deserialize\w+)\(\w+\);\r?\n\t\t\w+\.Dispose\(\);', "`t`tbyte[] data = `$1();`r`n`t`t`$2 obj = new `$2();`r`n`t	obj.`$3(data);`r`n`t	return obj;")
    $text = [regex]::Replace($text, '(?ms)(\t\tint err = JlNativeApi\.CallProcedure\(proc\);\r?\n)\t\terr = JlSerializationBuffer\.LoadNew\(proc, (\d+), err, out JlSerializationBuffer obj\);\r?\n\t\tJlNativeApi\.PostCall\(proc, err\);\r?\n\t\tGC\.KeepAlive\(this\);\r?\n\t\treturn obj;', '$1		byte[] data = JlSerializationBuffer.LoadBytes(proc, $2, err);$1		JlNativeApi.PostCall(proc, err);$1		GC.KeepAlive(this);$1		return data;')
    $text = [regex]::Replace($text, '(?ms)(\tpublic void Deserialize\w+\(byte\[] serializedItemHandle\)\r?\n\t\{)\r?\n\t\tDispose\(\);', '$1$2		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);$2		Dispose();')
    $text = $text.Replace('JlNativeApi.Store(proc, 0, serializedItemHandle);', 'JlNativeApi.Store(proc, 0, buffer);')
    $text = $text.Replace('GC.KeepAlive(serializedItemHandle);', 'GC.KeepAlive(buffer);')
    [IO.File]::WriteAllText($path, $text)
    Write-Host "Migrated serialization in $(Split-Path $path -Leaf)"
}

Get-ChildItem $runtime -Filter '*.cs' | ForEach-Object { Migrate-SerializationFile $_.FullName }

$deleteFiles = @(
    'JlBeadInspectionModel.cs', 'JlTextureInspectionModel.cs', 'JlTextureInspectionResult.cs',
    'JlComponentModel.cs', 'JlComponentTraining.cs',
    'JlScene3D.cs', 'JlSceneEngine.cs', 'JlStructuredLightModel.cs', 'JlSheetOfLightModel.cs',
    'JlSurfaceModel.cs', 'JlSurfaceMatchingResult.cs', 'JlDeformableSurfaceModel.cs', 'JlDeformableSurfaceMatchingResult.cs',
    'JlShapeModel3D.cs', 'JlStereoModel.cs', 'JlScatteredDataInterpolator.cs',
    'JlEncryptedItem.cs', 'JlImageSource.cs', 'JlBgEsti.cs', 'JlColorTransLUT.cs',
    'JlFunction1D.cs', 'JlCameraSetupModel.cs', 'JlMemoryBlock.cs', 'JlSerializedItem.cs',
    'JlDevInputParam.cs', 'JlDevInputParamObject.cs', 'JlDevInputParamTuple.cs', 'JlDevInputParamVector.cs',
    'JlDevOutputParam.cs', 'JlDevOutputParamObject.cs', 'JlDevOutputParamTuple.cs', 'JlDevOutputParamVector.cs'
)
foreach ($name in $deleteFiles) {
    $path = Join-Path $runtime $name
    if (Test-Path $path) { Remove-Item $path -Force; Write-Host "Deleted $name" }
}

function Test-RemoveMethod([string]$signature, [string]$fileName) {
    if ($signature -match 'Jl(BeadInspectionModel|TextureInspectionModel|TextureInspectionResult|ComponentModel|ComponentTraining|Scene3D|SceneEngine|StructuredLightModel|SheetOfLightModel|SurfaceModel|SurfaceMatchingResult|DeformableSurfaceModel|DeformableSurfaceMatchingResult|ShapeModel3D|StereoModel|ScatteredDataInterpolator|EncryptedItem|ImageSource|BgEsti|ColorTransLUT|Function1D|CameraSetupModel|MemoryBlock)\b') { return $true }
    if ($signature -match '\b(BeadInspection|TextureInspection|ComponentModel|ComponentTraining|Scene3d|SceneEngine|StructuredLight|SheetOfLight|SurfaceModel|SurfaceMatching|DeformableSurface|ShapeModel3d|StereoModel|ScatteredData|EncryptedItem|ImageSource|BgEsti|ColorTransLut|Function1d|CameraSetup|MemoryBlock)\b') { return $true }

    if ($fileName -eq 'JlCamPar.cs') {
        if ($signature -match 'ShapeModel3d|JlScene3D|CameraSetupModel|CameraSetupCamParam') { return $true }
    }

    if ($fileName -eq 'JlOperatorSet.cs') {
        if ($signature -match 'public static void \w*(BeadInspection|TextureInspection|ComponentModel|ComponentTraining|TrainingComponents|Scene3d|SceneEngine|StructuredLight|SheetOfLight|SurfaceModel|SurfaceMatching|DeformableSurface|ShapeModel3d|StereoModel|ScatteredData|EncryptedItem|ImageSource|BgEsti|ColorTransLut|Function1d|CameraSetup|MemoryBlock)\w*\(') { return $true }
        if ($signature -match 'public static void (CreateSerializedItemPtr|ClearSerializedItem|GetSerializedItemPtr|FreadSerializedItem|FwriteSerializedItem|EncryptSerializedItem|DecryptSerializedItem)\(') { return $true }
    }
    return $false
}

function Remove-TrimMethods([string]$path) {
    $fileName = Split-Path $path -Leaf
    $lines = [IO.File]::ReadAllLines($path)
    $n = $lines.Length
    $skip = New-Object bool[] $n
    $removed = 0
    $i = 0
    while ($i -lt $n) {
        if ($lines[$i] -match '^\t(public|internal|private|protected) ') {
            $start = $i
            while ($start -gt 0 -and ($lines[$start - 1] -match '^\t///' -or $lines[$start - 1] -match '^\t\[')) { $start-- }
            $j = $i
            $sig = $lines[$i]
            while ($j -lt $n -and $lines[$j] -notmatch '\{') {
                if ($j -ne $i) { $sig += ' ' + $lines[$j].Trim() }
                $j++
            }
            if ($j -ge $n) { $i++; continue }
            $depth = 0
            $k = $j
            do {
                $depth += ([regex]::Matches($lines[$k], '\{')).Count
                $depth -= ([regex]::Matches($lines[$k], '\}')).Count
                $k++
            } while ($k -lt $n -and $depth -gt 0)
            if (Test-RemoveMethod $sig $fileName) {
                for ($x = $start; $x -lt $k; $x++) { $skip[$x] = $true }
                $removed++
                $i = $k
                continue
            }
        }
        $i++
    }
    if ($removed -gt 0) {
        $sb = New-Object System.Text.StringBuilder
        for ($idx = 0; $idx -lt $n; $idx++) {
            if (-not $skip[$idx]) { [void]$sb.AppendLine($lines[$idx]) }
        }
        [IO.File]::WriteAllText($path, $sb.ToString().TrimEnd("`r", "`n") + "`r`n")
        Write-Host "Stripped $removed method(s) from $fileName"
    }
}

$stripTargets = @(
    'JlOperatorSet.cs', 'JlImage.cs', 'JlRegion.cs', 'JlXLD.cs', 'JlXLDCont.cs',
    'JlPose.cs', 'JlObjectModel3D.cs', 'JlMeasure.cs', 'JlMisc.cs', 'JlTuple.cs', 'JlHandle.cs'
) | ForEach-Object { Join-Path $runtime $_ }

foreach ($path in $stripTargets) { Remove-TrimMethods $path }

Write-Host 'Done.'
