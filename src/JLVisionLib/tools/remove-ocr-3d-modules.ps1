$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'JLVisionLib.Runtime'

$deleteFiles = @(
    'JlOCRBox.cs', 'JlOCRCnn.cs', 'JlOCRKnn.cs', 'JlOCRMlp.cs', 'JlOCRSvm.cs',
    'JlTextModel.cs', 'JlTextResult.cs', 'JlLexicon.cs',
    'JlObjectModel3D.cs', 'JlHomMat3D.cs', 'JlQuaternion.cs', 'JlDualQuaternion.cs'
)
foreach ($name in $deleteFiles) {
    $path = Join-Path $runtime $name
    if (Test-Path $path) { Remove-Item $path -Force; Write-Host "Deleted $name" }
}

function Test-RemoveOcr3dMethod([string]$signature, [string]$fileName) {
    if ($signature -match 'Jl(OCR(Box|Cnn|Knn|Mlp|Svm)|TextModel|TextResult|Lexicon|ObjectModel3D|HomMat3D|Quaternion|DualQuaternion)\b') { return $true }

    if ($signature -match '(?i)(OcrClass|OcrWord|DoOcr|TrainfOcr|CreateOcr|ReadOcr|WriteOcr|SerializeOcr|DeserializeOcr|ClearOcr|GetParamsOcr|GetFeaturesOcr|GetPrepInfoOcr|ReduceOcr|SelectFeatureSetTrainf)') { return $true }
    if ($signature -match '(?i)(TextModel|TextResult|Lexicon|FindText|CreateText|ApplyText|GetText|ClearText|SetText|QueryText|ReadText|WriteText|AppendText|SegmentText)') { return $true }

    if ($signature -match '(?i)(ObjectModel3d|HomMat3d|Quaternion|DualQuat|QuatTo|QuatCompose|PoseToQuat|QuatToPose|VectorToHomMat3d|RegisterObjectModel3d|GenBoxObjectModel3d|GenSphereObjectModel3d|GenCylinderObjectModel3d|SceneFlow|RenderObjectModel3d|ProjectObjectModel3d|ObjectModel3dToXyz|CamParPoseToHomMat3d|ReduceObjectModel3d|SurfaceNormalsObjectModel3d|TriangulateObjectModel3d|FitPrimitivesObjectModel3d|SegmentObjectModel3d|SmoothObjectModel3d|SimplifyObjectModel3d|DistanceObjectModel3d|UnionObjectModel3d|SampleObjectModel3d|SelectObjectModel3d|ConnectionObjectModel3d|SelectPointsObjectModel3d|ConvexHullObjectModel3d|IntersectPlaneObjectModel3d|AreaObjectModel3d|MaxDiameterObjectModel3d|MomentsObjectModel3d|VolumeObjectModel3d|SmallestBoundingBoxObjectModel3d|SmallestSphereObjectModel3d|SetObjectModel3d|GetObjectModel3d|ClearObjectModel3d|CopyObjectModel3d|PrepareObjectModel3d|FindBox3d|PoseToHomMat3d)') {
        return $true
    }

    if ($signature -match 'implicit operator JlHomMat3D') { return $true }

    return $false
}

function Remove-Ocr3dMethods([string]$path) {
    $fileName = Split-Path $path -Leaf
    if (-not (Test-Path $path)) { return }
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
            if (Test-RemoveOcr3dMethod $sig $fileName) {
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
    'JlOperatorSet.cs', 'JlImage.cs', 'JlRegion.cs', 'JlCamPar.cs', 'JlPose.cs'
) | ForEach-Object { Join-Path $runtime $_ }

foreach ($path in $stripTargets) { Remove-Ocr3dMethods $path }

Write-Host 'Done.'
