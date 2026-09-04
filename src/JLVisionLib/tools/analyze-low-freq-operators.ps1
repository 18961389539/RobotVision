$ErrorActionPreference = 'Stop'
$path = Join-Path (Split-Path $PSScriptRoot -Parent) 'JLVisionLib.Runtime\JlOperatorSet.cs'
$names = [System.Collections.Generic.List[string]]::new()
foreach ($line in [IO.File]::ReadAllLines($path)) {
    if ($line -match '^\tpublic static void (\w+)\(') { $names.Add($Matches[1]) }
}

$groups = [ordered]@{
    'OCR-Remnants' = 'Ocr|DeepOcr|Trainf|ProtectOcr|SegmentCharacters|AppendOcr|CloseOcr'
    'Extended-Match' = 'Deformable|Descriptor|Variation|Component|GenericShape|PlanarUncalib'
    'Exhaustive-ProjMatch' = 'Exhaustive|ProjMatch|ProjHom'
    'Hough' = 'Hough'
    'PolarTransform' = 'PolarTrans'
    'Fuzzy' = 'Fuzzy'
    'Skeleton-MorphAdv' = 'Skeleton|Minkowski|MorphSkiz|MorphSkeleton|MorphHat|HammingChange|SplitSkeleton'
    'Paint' = 'Paint|Overpaint'
    'Noise-Defocus-Grid' = 'GenGrid|AddNoise|RemoveNoise|GenPsf|SimulateDefocus|Defocus'
    'Optics-FFT-Texture-Remnants' = 'Optical|Depth|Focus|Flow|Coherence|Phase|Power|Energy|Bandpass|Wavelet|Shock|Gabor|Fft|Rft|Texture|Cooc'
    'Calib-Camera-Remnants' = 'Calib|CamPar|Camera|HandEye|Caltab|Binocular|Disparity|Stereo|SelfCalibration|Radiometric'
    'Learn-Classify' = 'Learn|Classif|Quality|Deviation|Entropy|HistoToThresh|NdimNorm'
    '3D-Remnants' = 'ObjectModel|HomMat3d|Quaternion|Pose3d|Box3d|Scene|Surface'
    'Serialize-IO' = '^Serialize|^Deserialize|^Read|^Write'
    'Edge-Line-Circle' = 'Edges|LinesGauss|CirclesGauss|EllipsesGauss|Corner|ZeroCrossing|SubPix'
    'Image-Compose' = 'Compose|Decompose|Tile|CropDomain|FullDomain|Channels|Interleave'
    'Region-Features' = 'SelectShape|AreaCenter|Moments|Convexity|Diameter|Compactness|Rectangularity|Roundness|Eccentricity|SmallestRectangle|InnerRectangle|InnerCircle'
    'XLD-Advanced' = 'Xld|Contour|DistTrans|AffineTransContour|Union.*Xld|Difference.*Xld|ClipEndPoints|ClosestPoint'
    'Measure-Fit-Residual' = '^Measure|Metrology|FitLine|FitCircle|FitEllipse|FitRectangle|DistancePp|DistancePl|AngleLl|AngleLx'
    'Shape-NCC-Core' = 'CreateShapeModel|FindShapeModel|ClearShapeModel|CreateNccModel|FindNccModel|CreateScaled|FindScaled|DetermineShape|DetermineNcc'
    'Tuple-Ops' = '^Tuple'
    'HomMat2D-Pose' = 'HomMat2d|AffineTrans|VectorTo|PoseTo|ProjectiveTrans|RigidTrans'
    'Basic-Image' = '^ReadImage|^WriteImage|^GenImage|^CropImage|^ZoomImage|^RotateImage|^MirrorImage|^Threshold|DynThreshold|Gauss|Median|Mean|Connection|Union1|Intersection|Dilation|Erosion'
}

$used = @{}
Write-Host "Total: $($names.Count)`n"
foreach ($k in $groups.Keys) {
    $m = @($names | Where-Object { $_ -match $groups[$k] -and -not $used.ContainsKey($_) })
    foreach ($x in $m) { $used[$x] = $true }
    if ($m.Count -gt 0) {
        $sample = ($m | Select-Object -First 3) -join ', '
        Write-Host ("{0,-30} {1,4}  {2}" -f $k, $m.Count, $sample)
    }
}
$rest = @($names | Where-Object { -not $used.ContainsKey($_) })
Write-Host ("`n{0,-30} {1,4}" -f 'Unclassified', $rest.Count)
$rest | Group-Object { if ($_ -match '^([A-Z][a-z]+)') { $Matches[1] } else { 'Other' } } | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object {
    Write-Host ("  {0,-20} {1}" -f $_.Name, $_.Count)
}
