$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'JLVisionLib.Runtime'

$deleteFiles = @('JlCalibData.cs', 'JlCamPar.cs')
foreach ($name in $deleteFiles) {
    $path = Join-Path $runtime $name
    if (Test-Path $path) { Remove-Item $path -Force; Write-Host "Deleted $name" }
}

function Test-RemoveCalibVisionMethod([string]$signature, [string]$fileName) {
    if ($signature -match 'Jl(CalibData|CamPar)\b') { return $true }

    if ($signature -match '(?i)(CalibData|CamPar|CameraCalibration|HandEyeCalibration|Caltab|SimCaltab|FindMarksAndPose|RadialDistortion|Telecentric|GenCamPar|CamMatToCamPar|ImagePointsToWorld|WorldPlaneToImage|CreateCalib|SetCalib|GetCalib|FindCalib|PlanarCalib|CalibDescriptor|CalibDeformable|StationaryCamera|RadiometricSelf|SelfCalibration)') { return $true }

    if ($signature -match '(?i)(Binocular|Disparity|Stereo|RelPose|FundamentalMatrix|GenBinocular|IntersectLinesOfSight|MatchRelPose|DisparityImage|DisparityTo|DistanceToDisparity|VectorToRelPose)') { return $true }

    if ($signature -match '(?i)(Fft|Rft|Wavelet|TextureLaws|Cooc|GenCooc|OpticalFlow|DepthFromFocus|ShockFilter|Gabor|GenGabor|ConvolGabor|EnergyGabor|PhaseCorrelation|CorrelationFft|ConvolFft|OptimizeFft|OptimizeRft|DeserializeFft|SerializeFft|ReadFft|WriteFft|GenBandfilter|GenLowpass|GenHighpass|PowerLn|PowerByte|PhaseDeg|PhaseRad|Bandpass|Anisotropic|GuidedFilter|Bilateral|Monotony|UnwarpImageVectorField|InpaintingTexture|Radiometric)') { return $true }

    if ($signature -match '(?i)(FreiAmp|FreiDir|RobinsonAmp|RobinsonDir|KirschAmp|KirschDir|Prewitt|DerivateGauss)') { return $true }

    return $false
}

function Remove-CalibVisionMethods([string]$path) {
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
            if (Test-RemoveCalibVisionMethod $sig $fileName) {
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
    'JlOperatorSet.cs', 'JlImage.cs', 'JlRegion.cs', 'JlPose.cs', 'JlHomMat2D.cs',
    'JlXLD.cs', 'JlXLDCont.cs', 'JlDeformableModel.cs', 'JlDescriptorModel.cs', 'JlMisc.cs'
) | ForEach-Object { Join-Path $runtime $_ }

foreach ($path in $stripTargets) { Remove-CalibVisionMethods $path }

Write-Host 'Done.'
