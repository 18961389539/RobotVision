$ErrorActionPreference = 'Stop'
$path = Join-Path (Split-Path $PSScriptRoot -Parent) 'JLVisionLib.Runtime\JlOperatorSet.cs'
$names = [System.Collections.Generic.List[string]]::new()
foreach ($line in [IO.File]::ReadAllLines($path)) {
    if ($line -match '^\tpublic static void (\w+)\(') {
        $names.Add($Matches[1])
    }
}
Write-Host "Total operators: $($names.Count)"

$categories = [ordered]@{
    'ShapeMatch' = 'Shape|Ncc|Descriptor|Deformable|Variation|CreateTemplate|CreatePlanar|Component|GenericShape'
    'ImageIO' = '^ReadImage|^WriteImage|^GenImage|^CropImage|^ZoomImage|^RotateImage|^MirrorImage|^AffineTransImage|^Compose|^Decompose|^TileImages|^ConcatObj'
    'ImageFilter' = 'Gray|Rgb|Hsv|Scale|Emphasize|Illuminate|Mean|Median|Gauss|Smooth|Sharpen|Contrast|Hist|Equal|Invert|Binomial|Laplace|Sobel|Prewitt|Roberts|Derivate|Bandpass|Anisotropic|Guided|Bilateral'
    'Threshold' = 'Threshold|DynThreshold|VarThreshold|Watershed|Regiongrowing|Label|Segment'
    'EdgeFFT' = 'Edges|Corner|ZeroCrossing|Lines|Circles|Ellipses|FFT|Fft|Wavelet|Coherence|Phase|Power|Energy'
    'RegionMorph' = '^GenRegion|^Union|^Intersection|^Difference|^Connection|^SelectShape|^FillUp|^Erosion|^Dilation|^Opening|^Closing|^TopHat|Region|Morph|Skeleton|Convexity|Hamming'
    'XLD' = 'Xld|XLD|Contour|SubPix|DistTrans|GenContour'
    'Measure' = '^Measure|Metrology|FitLine|FitCircle|FitEllipse|FitRect|DistancePp|DistancePl|Angle|Diameter|Length'
    'Calib' = 'Calib|CamPar|Camera|HandEye|Radial|Telecentric|Binocular|Stereo|Disparity'
    'Geometry' = 'HomMat2d|Affine|VectorTo|PoseTo|Projective|Rigid|Project'
    'Matrix' = '^CreateMatrix|^MultMatrix|^InvertMatrix|^TransposeMatrix|Solve|Eigen|Svd|Determinant'
    'RemovedCheck' = 'Ocr|BarCode|DataCode|TextModel|TextResult|Lexicon|ObjectModel3d|HomMat3d|Quaternion|ClassGmm|ClassKnn|Dl|Socket|Serial|Framegrabber'
    'TupleObj' = '^CopyObj|^ClearObj|^TestEqual|^Concat|^Select|^Insert|^Remove|^Count|^Tuple'
}

$assigned = @{}
foreach ($cat in $categories.Keys) {
    $pat = $categories[$cat]
    $matched = @($names | Where-Object { $_ -match $pat })
    $assigned[$cat] = $matched
    if ($matched.Count -gt 0) {
        Write-Host ""
        Write-Host "[$cat] $($matched.Count)"
        $matched | Select-Object -First 6 | ForEach-Object { Write-Host "  $_" }
        if ($matched.Count -gt 6) { Write-Host "  ... +$($matched.Count - 6) more" }
    }
}

$allMatched = @($assigned.Values | ForEach-Object { $_ } | Select-Object -Unique)
$unmatched = @($names | Where-Object { $_ -notin $allMatched })
Write-Host ""
Write-Host "[Other] $($unmatched.Count)"
$unmatched | Group-Object { if ($_ -match '^([A-Z][a-z]+)') { $Matches[1] } else { 'Other' } } | Sort-Object Count -Descending | Select-Object -First 25 | ForEach-Object {
    Write-Host ("  {0,-20} {1}" -f $_.Name, $_.Count)
}
