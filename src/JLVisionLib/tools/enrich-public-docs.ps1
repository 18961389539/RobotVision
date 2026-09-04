param(
    [string[]]$Files,
    [switch]$Force,
    [switch]$OperatorsOnly
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'JLVisionLib.Runtime'

$defaultFiles = @(
    'JlException.cs', 'JlOperatorException.cs', 'JlMisc.cs', 'JlPose.cs', 'JlMeasure.cs',
    'JlNCCModel.cs', 'JlHandle.cs', 'JlObject.cs', 'JlHomMat2D.cs', 'JlShapeModel.cs',
    'JlMetrologyModel.cs', 'JlMatrix.cs', 'JlXLD.cs', 'JlXLDPoly.cs', 'JlXLDPara.cs',
    'JlXLDModPara.cs', 'JlXLDExtPara.cs', 'JlXLDDistTrans.cs', 'JlXLDCont.cs',
    'JlRegion.cs', 'JlImage.cs', 'JlTuple.cs', 'JlData.cs', 'JlOperatorSet.cs'
)

if ($Files) {
    $targetFiles = $Files | ForEach-Object {
        if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $runtime $_ }
    }
} else {
    $targetFiles = $defaultFiles | ForEach-Object { Join-Path $runtime $_ }
}

# Longer phrases first. Applied to English <summary> text.
$script:PhraseMap = [ordered]@{
    'Prepare the extraction of straight edges perpendicular to an annular arc' = '准备提取垂直于圆环弧的直线边缘（圆弧卡尺）'
    'Prepare the extraction of straight edges perpendicular to a rectangle' = '准备提取垂直于矩形的直线边缘（矩形卡尺）'
    'Smooth image using a mean filter with arbitrary mask' = '使用任意形状掩膜的均值滤波平滑图像'
    'Smooth an image with an arbitrary rank mask' = '使用任意秩掩膜平滑图像'
    'Read an image with different file formats' = '读取多种文件格式的图像'
    'Create an image with constant gray value' = '创建灰度为常数的图像'
    'Create an image from a pointer to the pixels' = '由像素指针创建图像'
    'Create an uninitialized iconic object' = '创建未初始化的图像对象'
    'Returns the iconic object(s) at the specified index' = '返回指定索引处的图像对象'
    'Provides access to the internally used tuple data' = '访问内部使用的元组数据'
    'Provides access to the value at the specified index' = '按索引访问元素'
    'Provides access to tuple elements at the specified indices' = '按索引访问元组元素'
    'Provides access to the tuple element at the specified index' = '按索引访问元组元素'
    'Compute the union of cotangential contours' = '合并切线方向接近的轮廓'
    'Create control data of a NURBS curve that interpolates given points' = '根据给定点生成 NURBS 插值曲线的控制数据'
    'Find the best matches of a shape model in an image' = '在图像中查找形状模型的最佳匹配'
    'Create a shape model for matching' = '创建用于匹配的形状模型'
    'Read a shape model from a file' = '从文件读取形状模型'
    'Write a shape model to a file' = '将形状模型写入文件'
    'Clear a shape model' = '释放形状模型'
    'Find the best matches of an NCC model in an image' = '在图像中查找 NCC 模型的最佳匹配'
    'Create an NCC model for matching' = '创建用于匹配的 NCC 模型'
    'Class grouping all Vision operators' = '汇总全部视觉算子的静态入口'
    'Class grouping methods belonging to no other Vision class' = '不属于其他视觉类型的杂项方法'
    'Represents an instance of an image object(-array)' = '表示图像对象（可含数组）'
    'Image restoration by Wiener filtering' = '用维纳滤波恢复图像'
    'Generate an impulse response of a (linearly) motion blurring' = '生成线性运动模糊的冲激响应'
}

$script:WordPhrases = [ordered]@{
    'Compute the union of' = '计算以下对象的并集：'
    'Compute the intersection of' = '计算以下对象的交集：'
    'Compute the difference of' = '计算以下对象的差集：'
    'Compute the symmetric difference of' = '计算以下对象的对称差：'
    'Create control data of' = '创建控制数据：'
    'that interpolates given points' = '（对给定点插值）'
    'with arbitrary mask' = '（任意形状掩膜）'
    'with an arbitrary rank mask' = '（任意秩掩膜）'
    'using a mean filter' = '使用均值滤波'
    'using a gauss filter' = '使用高斯滤波'
    'using a median filter' = '使用中值滤波'
    'in an image' = '（在图像中）'
    'from a file' = '（从文件）'
    'to a file' = '（到文件）'
    'of a shape model' = '形状模型'
    'of an NCC model' = 'NCC 模型'
    'of a tuple' = '元组'
    'of two input tuples' = '两个输入元组'
    'straight edges' = '直线边缘'
    'annular arc' = '圆环弧'
    'cotangential contours' = '共切线轮廓'
    'collinear contours' = '共线轮廓'
    'adjacent contours' = '相邻轮廓'
    'closed contours' = '闭合轮廓'
    'closed polygons' = '闭合多边形'
    'NURBS curve' = 'NURBS 曲线'
    'shape model' = '形状模型'
    'NCC model' = 'NCC 模型'
    'measure object' = '测量对象'
    'metrology model' = '计量模型'
    'gray value' = '灰度值'
    'file formats' = '文件格式'
    'input image' = '输入图像'
    'input tuple' = '输入元组'
    'output image' = '输出图像'
    'filter mask' = '滤波掩膜'
    'iconic object' = '图像对象'
    'tuple data' = '元组数据'
    'subpixel' = '亚像素'
    'connected components' = '连通域'
    'region of interest' = '感兴趣区域'
    'distance transform' = '距离变换'
    'homography' = '单应性'
    'affine transformation' = '仿射变换'
    'projective transformation' = '投影变换'
    'mean filter' = '均值滤波'
    'gauss filter' = '高斯滤波'
    'median filter' = '中值滤波'
    'rank mask' = '秩掩膜'
}

$script:WordMap = [ordered]@{
    'Compute' = '计算'; 'Create' = '创建'; 'Read' = '读取'; 'Write' = '写入'
    'Find' = '查找'; 'Clear' = '释放'; 'Get' = '获取'; 'Set' = '设置'
    'Gen' = '生成'; 'Generate' = '生成'; 'Convert' = '转换'; 'Select' = '选择'
    'Fit' = '拟合'; 'Measure' = '测量'; 'Smooth' = '平滑'; 'Threshold' = '阈值分割'
    'Serialize' = '序列化'; 'Deserialize' = '反序列化'; 'Clone' = '克隆'
    'Train' = '训练'; 'Compare' = '比较'; 'Crop' = '裁剪'; 'Rotate' = '旋转'
    'Mirror' = '镜像'; 'Zoom' = '缩放'; 'Dilation' = '膨胀'; 'Erosion' = '腐蚀'
    'Opening' = '开运算'; 'Closing' = '闭运算'; 'Connection' = '连通域分析'
    'Union' = '合并'; 'Intersection' = '求交'; 'Difference' = '求差'
    'Affine' = '仿射变换'; 'Projective' = '投影变换'; 'Distance' = '距离'
    'Angle' = '角度'; 'Paint' = '绘制'; 'Skeleton' = '骨架'
    'Prepare' = '准备'; 'Extract' = '提取'; 'Return' = '返回'; 'Returns' = '返回'
    'Test' = '测试'; 'Determine' = '确定'; 'Calculate' = '计算'; 'Store' = '存储'
    'the' = ''; 'a' = ''; 'an' = ''; 'of' = ''; 'for' = '用于'; 'to' = ''
    'with' = '使用'; 'using' = '使用'; 'from' = '从'; 'in' = '在'; 'by' = '通过'
    'and' = '和'; 'or' = '或'; 'into' = '为'; 'as' = '作为'; 'on' = '在'
    'image' = '图像'; 'images' = '图像'; 'region' = '区域'; 'regions' = '区域'
    'contour' = '轮廓'; 'contours' = '轮廓'; 'polygon' = '多边形'; 'polygons' = '多边形'
    'tuple' = '元组'; 'tuples' = '元组'; 'model' = '模型'; 'models' = '模型'
    'matrix' = '矩阵'; 'pose' = '位姿'; 'handle' = '句柄'; 'object' = '对象'
    'mask' = '掩膜'; 'filter' = '滤波'; 'edge' = '边缘'; 'edges' = '边缘'
    'line' = '直线'; 'circle' = '圆'; 'ellipse' = '椭圆'; 'rectangle' = '矩形'
    'point' = '点'; 'points' = '点'; 'pixel' = '像素'; 'pixels' = '像素'
    'file' = '文件'; 'matching' = '匹配'; 'best' = '最佳'; 'matches' = '匹配结果'
    'given' = '给定'; 'input' = '输入'; 'output' = '输出'; 'value' = '值'
    'values' = '值'; 'parameter' = '参数'; 'parameters' = '参数'
    'instance' = '实例'; 'uninitialized' = '未初始化'; 'constant' = '常数'
    'gray' = '灰度'; 'specified' = '指定'; 'index' = '索引'; 'indices' = '索引'
    'internally' = '内部'; 'used' = '使用的'; 'access' = '访问'; 'element' = '元素'
    'elements' = '元素'; 'data' = '数据'; 'control' = '控制'; 'curve' = '曲线'
    'interpolates' = '插值'; 'perpendicular' = '垂直'; 'extraction' = '提取'
    'straight' = '直线'; 'annular' = '环形'; 'arc' = '圆弧'
}

$script:NameTokenZh = [ordered]@{
    'Cotangential' = '共切线'; 'Collinear' = '共线'; 'Adjacent' = '相邻'; 'Closed' = '闭合'
    'Contours' = '轮廓'; 'Contour' = '轮廓'; 'Polygons' = '多边形'; 'Polygon' = '多边形'
    'Xld' = 'XLD'; 'Nurbs' = 'NURBS'; 'Interp' = '插值'; 'Union' = '合并'
    'Intersection' = '求交'; 'Difference' = '差集'; 'Symm' = '对称'; 'Gen' = '生成'
    'Find' = '查找'; 'Create' = '创建'; 'Clear' = '释放'; 'Read' = '读取'; 'Write' = '写入'
    'Shape' = '形状'; 'Ncc' = 'NCC'; 'Model' = '模型'; 'Models' = '模型'
    'Image' = '图像'; 'Images' = '图像'; 'Region' = '区域'; 'Regions' = '区域'
    'Mean' = '均值'; 'Gauss' = '高斯'; 'Median' = '中值'; 'Threshold' = '阈值'
    'Measure' = '测量'; 'Metrology' = '计量'; 'Fit' = '拟合'; 'Line' = '直线'
    'Circle' = '圆'; 'Ellipse' = '椭圆'; 'Rectangle' = '矩形'; 'Affine' = '仿射'
    'Trans' = '变换'; 'Hom' = '齐次'; 'Mat' = '矩阵'; 'Pose' = '位姿'
    'Tuple' = '元组'; 'Select' = '选择'; 'Obj' = '对象'; 'Clone' = '克隆'
    'Serialize' = '序列化'; 'Deserialize' = '反序列化'; 'Get' = '获取'; 'Set' = '设置'
    'Crop' = '裁剪'; 'Rotate' = '旋转'; 'Mirror' = '镜像'; 'Zoom' = '缩放'
    'Dilation' = '膨胀'; 'Erosion' = '腐蚀'; 'Opening' = '开运算'; 'Closing' = '闭运算'
    'Connection' = '连通域'; 'Skeleton' = '骨架'; 'Paint' = '绘制'; 'Distance' = '距离'
    'Angle' = '角度'; 'Convert' = '转换'; 'Projective' = '投影'; 'Train' = '训练'
    'Compare' = '比较'; 'Prepare' = '准备'; 'Edges' = '边缘'; 'Sub' = '亚'; 'Pix' = '像素'
    'Scaled' = '缩放'; 'Local' = '局部'; 'Gray' = '灰度'; 'Rgb' = 'RGB'
    'Domain' = '定义域'; 'Fill' = '填充'; 'Area' = '面积'; 'Center' = '中心'
    'Moments' = '矩'; 'Inner' = '内接'; 'Smallest' = '最小外接'; 'Points' = '点'
    'Smooth' = '平滑'; 'Clip' = '裁剪'; 'Sort' = '排序'; 'Merge' = '合并'
    'Parallel' = '平行'; 'Parallels' = '平行线'; 'Roads' = '道路'; 'Histo' = '直方图'
    'Wiener' = '维纳'; 'Filter' = '滤波'; 'Psf' = '点扩散'; 'Motion' = '运动'
    'Defocus' = '离焦'; 'Noise' = '噪声'; 'Matrix' = '矩阵'; 'Invert' = '求逆'
    'Transpose' = '转置'; 'Solve' = '求解'; 'Eigen' = '特征'; 'Vector' = '向量'
    'Field' = '场'; 'Item' = '索引器'; 'Raw' = '原始'; 'Data' = '数据'
    'Handle' = '句柄'; 'Exception' = '异常'; 'Operator' = '算子'
}

function Test-HasEditorBrowsableNever([string[]]$lines, [int]$index) {
    for ($i = $index - 1; $i -ge 0 -and $i -ge $index - 10; $i--) {
        if ($lines[$i] -match 'EditorBrowsable\(EditorBrowsableState\.Never\)') { return $true }
        if ($lines[$i] -match '^\t(public|internal|private|protected|namespace|class|struct|enum|\[assembly)') { break }
    }
    return $false
}

function Get-ClassName([string[]]$lines) {
    foreach ($line in $lines) {
        if ($line -match '^\s*public (?:sealed |abstract |static )*class (\w+)') { return $Matches[1] }
    }
    return 'JlObject'
}

function Get-MemberSignature([string[]]$lines, [int]$index) {
    $sb = New-Object System.Text.StringBuilder
    for ($i = $index; $i -lt [Math]::Min($index + 12, $lines.Length); $i++) {
        [void]$sb.Append(' ')
        [void]$sb.Append($lines[$i].Trim())
        if ($lines[$i] -match '\{|=>') { break }
    }
    return $sb.ToString().Trim()
}

function Get-MemberName([string]$signature, [string]$className) {
    if ($signature -match 'operator\s+(\S+)') { return "operator $($Matches[1])" }
    if ($signature -match 'implicit operator|explicit operator') { return 'conversion operator' }
    if ($className -and $signature -match "public\s+(?:new\s+)?$className\s*\(") { return $className }
    if ($signature -match '\bthis\s*\[') { return 'Item' }
    if ($signature -match '^\s*public\s+(?:static\s+)?(?:new\s+)?(?:readonly\s+)?(?:const\s+)?[\w<>\[\],\s]+\s+(\w+)\s*\(') { return $Matches[1] }
    if ($signature -match '^\s*public\s+(?:static\s+)?(?:new\s+)?(?:readonly\s+)?[\w<>\[\],\s]+\s+(\w+)\s*\{') { return $Matches[1] }
    return 'Member'
}

function Get-ParamNames([string]$signature) {
    if ($signature -notmatch '\((.*)\)\s*(\{|=>|;|$)') {
        if ($signature -notmatch '\((.*)\)') { return @() }
    }
    $inner = $Matches[1]
    if ([string]::IsNullOrWhiteSpace($inner)) { return @() }
    $names = New-Object System.Collections.Generic.List[string]
    foreach ($part in ($inner -split ',')) {
        $p = $part.Trim()
        if ($p -match '^(?:out|ref|in)\s+') { $p = ($p -replace '^(?:out|ref|in)\s+', '').Trim() }
        if ($p -match '\s+(\w+)\s*$') { [void]$names.Add($Matches[1]) }
    }
    return ,$names.ToArray()
}

function Get-CommentBlockStart([string[]]$lines, [int]$index) {
    $start = $index
    while ($start -gt 0 -and ($lines[$start - 1] -match '^\t///' -or $lines[$start - 1] -match '^\t\[')) { $start-- }
    return $start
}

function Get-ExistingSummary([string[]]$lines, [int]$index) {
    $start = Get-CommentBlockStart $lines $index
    $inSummary = $false
    $parts = New-Object System.Collections.Generic.List[string]
    for ($i = $start; $i -lt $index; $i++) {
        $raw = $lines[$i]
        if ($raw -match '<summary>') { $inSummary = $true }
        if ($inSummary) {
            $t = $raw -replace '^\t///\s*', '' -replace '</?summary>', ''
            $t = $t.Trim()
            if ($t -and $t -notmatch 'Instance represents' -and $t -notmatch 'Modified instance represents') {
                [void]$parts.Add($t)
            }
            if ($raw -match '</summary>') { break }
        }
    }
    return (($parts -join ' ').Trim())
}

function Get-ParamDocMap([string[]]$lines, [int]$index) {
    $start = Get-CommentBlockStart $lines $index
    $map = @{}
    for ($i = $start; $i -lt $index; $i++) {
        if ($lines[$i] -match 'param name="(\w+)"') {
            $name = $Matches[1]
            $rest = $lines[$i]
            $def = $null
            if ($rest -match 'Default:\s*(\[[^\]]*\]|"[^"]*"|-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)') {
                $def = $Matches[1]
            }
            $map[$name] = @{ Default = $def; Line = $rest }
        }
    }
    return $map
}

function Convert-DefaultToCode([string]$def, [string]$typeHint) {
    if ([string]::IsNullOrWhiteSpace($def)) { return $null }
    $d = $def.Trim()
    if ($d -eq '[]') { return 'new JlTuple()' }
    if ($d.StartsWith('"') -and $d.EndsWith('"')) { return $d }
    if ($d -match '^-?\d') { return $d }
    if ($typeHint -match 'string') { return '"' + ($d.Trim('"')) + '"' }
    return $d
}

function Convert-SummaryToZh([string]$en) {
    if ([string]::IsNullOrWhiteSpace($en)) { return $null }
    $s = $en.Trim().TrimEnd('.')
    $s = [regex]::Replace($s, '\s*(Modified )?Instance represents:.*$', '')
    $s = $s.Trim()
    if (-not $s) { return $null }

    foreach ($k in $script:PhraseMap.Keys) {
        if ($s -eq $k -or $s.StartsWith($k)) {
            return $script:PhraseMap[$k]
        }
    }

    $zh = $s
    foreach ($k in $script:WordPhrases.Keys) {
        if ($zh.IndexOf($k, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $zh = [regex]::Replace($zh, [regex]::Escape($k), $script:WordPhrases[$k], 'IgnoreCase')
        }
    }
    foreach ($k in $script:WordMap.Keys) {
        $zh = [regex]::Replace($zh, ('\b' + [regex]::Escape($k) + '\b'), $script:WordMap[$k], 'IgnoreCase')
    }
    $zh = [regex]::Replace($zh, '\s+', ' ').Trim().Trim('：', ':', '，', ',')
    $zh = $zh -replace '  +', ' '
    $latinWords = [regex]::Matches($zh, '[A-Za-z]{4,}')
    if ($latinWords.Count -ge 4) { return $null }
    if ([string]::IsNullOrWhiteSpace($zh)) { return $null }
    if (-not $zh.EndsWith('。')) { $zh += '。' }
    return $zh
}

function Split-CamelTokens([string]$name) {
    $s = [regex]::Replace($name, '(\d+)([A-Z])', '$1 $2')
    $s = [regex]::Replace($s, '([a-z0-9])([A-Z])', '$1 $2')
    $s = [regex]::Replace($s, '([A-Z]+)([A-Z][a-z])', '$1 $2')
    return @($s.Split(' ', [StringSplitOptions]::RemoveEmptyEntries))
}

function Get-NameZh([string]$memberName) {
    $parts = New-Object System.Collections.Generic.List[string]
    foreach ($tok in (Split-CamelTokens $memberName)) {
        if ($script:NameTokenZh.Contains($tok)) { [void]$parts.Add([string]$script:NameTokenZh[$tok]) }
        else { [void]$parts.Add($tok) }
    }
    return ($parts -join '')
}

function Get-Scene([string]$memberName, [string]$className, [string]$signature) {
    if ($className -match 'Exception') { return '异常捕获与错误码处理' }
    if ($memberName -eq $className) {
        if ($className -eq 'JlMeasure') { return '卡尺测量：创建测量对象后调用 MeasurePos / MeasurePairs' }
        if ($className -match 'Model') { return '创建或加载模型，供后续匹配或计量使用' }
        return "创建 $className 对象"
    }
    if ($signature -match '\bthis\s*\[') { return '按索引取出对象数组中的单个元素' }
    if ($memberName -match '^(Clone|CopyObj|ConcatObj|SelectObj|InsertObj|RemoveObj|ReplaceObj|TestEqualObj)$') {
        return '对象容器操作：复制、拼接或按索引存取'
    }
    if ($memberName -match 'ShapeModel|NccModel|FindShape|CreateShape|FindNcc|CreateNcc') {
        return '模板匹配定位（形状匹配或 NCC）'
    }
    if ($memberName -match 'Measure|Metrology') { return '尺寸检测与边缘定位' }
    if ($memberName -match 'Xld|Contour|Polygon|Nurbs|Parallel') { return '轮廓（XLD）生成、合并与几何拟合' }
    if ($memberName -match 'Threshold|DynThreshold|Connection|SelectShape|FillUp|Opening|Closing') {
        return '图像分割、连通域分析与区域筛选'
    }
    if ($memberName -match 'Dilation|Erosion|Skeleton|HitOrMiss|TopHat|BottomHat|Minkowski') {
        return '形态学处理'
    }
    if ($memberName -match 'Mean|Gauss|Median|Smooth|Convol|Rank|MeanImage') { return '图像滤波与预处理' }
    if ($memberName -match 'HomMat|AffineTrans|ProjectiveTrans|VectorToRigid|VectorAngleToRigid') {
        return '坐标变换、位姿对齐与几何校正'
    }
    if ($memberName -match '^Tuple') { return '元组数值与集合运算' }
    if ($memberName -match 'Matrix') { return '矩阵运算与线性求解' }
    if ($memberName -match 'Pose') { return '位姿表示与变换' }
    if ($memberName -match 'Serialize|Deserialize') { return '对象在内存中的序列化传递' }
    if ($memberName -match '^Read') { return '从文件加载图像、区域、模型或数据' }
    if ($memberName -match '^Write') { return '将图像、区域、模型或数据保存到文件' }
    if ($memberName -match 'Rgb|Gray|Channels|Compose|Decompose') { return '颜色空间与通道处理' }
    if ($memberName -match 'Crop|Zoom|Rotate|Mirror|Clip') { return '几何裁剪与尺寸变换' }
    if ($memberName -match 'Area|Moments|Compactness|InnerCircle|SmallestRectangle|Diameter') {
        return '区域或轮廓特征计算'
    }
    if ($memberName -match 'FitLine|FitCircle|FitEllipse|FitRectangle|EdgesSubPix') {
        return '亚像素边缘提取与几何拟合'
    }
    if ($memberName -match 'Distance|Angle|IntersectionLl|ProjectionPl') { return '点线几何量测' }
    if ($memberName -eq 'Item' -or $memberName -eq 'RawData') { return '访问内部数据或按下标取值' }
    return $null
}

function Get-RelatedOperators([string]$memberName) {
    $related = New-Object System.Collections.Generic.List[string]
    function Add-Rel([string[]]$items) { foreach ($x in $items) { if ($x -ne $memberName) { [void]$related.Add($x) } } }

    if ($memberName -match 'UnionCotangential|UnionCollinear|UnionAdjacent|UnionCocircular') {
        Add-Rel @('UnionCollinearContoursXld', 'UnionAdjacentContoursXld', 'SelectContoursXld')
    }
    elseif ($memberName -match 'CreateShapeModel|CreateScaledShapeModel') { Add-Rel @('FindShapeModel', 'GetShapeModelContours', 'ClearShapeModel') }
    elseif ($memberName -match 'FindShapeModel|FindScaledShapeModel') { Add-Rel @('CreateShapeModel', 'VectorAngleToRigid', 'GetShapeModelContours') }
    elseif ($memberName -match 'ReadShapeModel') { Add-Rel @('FindShapeModel', 'WriteShapeModel', 'ClearShapeModel') }
    elseif ($memberName -match 'WriteShapeModel') { Add-Rel @('ReadShapeModel', 'CreateShapeModel') }
    elseif ($memberName -match 'ClearShapeModel') { Add-Rel @('CreateShapeModel') }
    elseif ($memberName -match 'CreateNccModel') { Add-Rel @('FindNccModel', 'ClearNccModel') }
    elseif ($memberName -match 'FindNccModel') { Add-Rel @('CreateNccModel', 'ClearNccModel') }
    elseif ($memberName -match 'ReadNccModel') { Add-Rel @('FindNccModel', 'WriteNccModel') }
    elseif ($memberName -match 'GenMeasure|MeasurePos|MeasurePairs|TranslateMeasure|CloseMeasure') {
        Add-Rel @('GenMeasureRectangle2', 'MeasurePos', 'MeasurePairs', 'CloseMeasure')
    }
    elseif ($memberName -match 'ReadImage') { Add-Rel @('Rgb1ToGray', 'Threshold', 'CropDomain') }
    elseif ($memberName -match '^Threshold$|DynThreshold') { Add-Rel @('Connection', 'SelectShape', 'FillUp') }
    elseif ($memberName -match '^Connection$') { Add-Rel @('SelectShape', 'AreaCenter', 'FillUp') }
    elseif ($memberName -match 'SelectShape') { Add-Rel @('Connection', 'AreaCenter') }
    elseif ($memberName -match 'MeanImage|GaussImage|MedianImage|MeanImageShape') { Add-Rel @('Threshold', 'GaussFilter', 'MedianImage') }
    elseif ($memberName -match 'EdgesSubPix|EdgesImage') { Add-Rel @('SelectContoursXld', 'FitLineContourXld') }
    elseif ($memberName -match 'FitLineContourXld|FitCircleContourXld|FitEllipseContourXld|FitRectangle2ContourXld') {
        Add-Rel @('EdgesSubPix', 'SelectContoursXld', 'GenContourPolygonXld')
    }
    elseif ($memberName -match 'VectorAngleToRigid|VectorToRigid') { Add-Rel @('AffineTransImage', 'AffineTransRegion') }
    elseif ($memberName -match 'AffineTransImage') { Add-Rel @('AffineTransRegion', 'HomMat2dIdentity') }
    elseif ($memberName -match 'GenContourRegionXld|GenContourPolygonXld') { Add-Rel @('SelectContoursXld', 'FitLineContourXld') }
    elseif ($memberName -match 'Rgb1ToGray|RgbToGray') { Add-Rel @('Threshold', 'MeanImage') }
    return $related
}

function Get-ParamPartMap([string]$signature) {
    $map = @{}
    if ($signature -notmatch '\((.*)\)') { return $map }
    $inner = $Matches[1]
    if ([string]::IsNullOrWhiteSpace($inner)) { return $map }
    foreach ($part in ($inner -split ',')) {
        $p = $part.Trim()
        if ($p -match '\s+(\w+)\s*$') {
            $map[$Matches[1]] = $p
        }
    }
    return $map
}

function Get-ParamTypeHint([string]$signature, [string]$paramName) {
    $parts = Get-ParamPartMap $signature
    if ($parts.ContainsKey($paramName)) { return [string]$parts[$paramName] }
    return ''
}

function Get-FallbackSample([string]$paramName, [string]$typeHint, [string]$memberName) {
    $n = $paramName.ToLowerInvariant()
    if ($n -eq 'filename' -or $n -like '*filename') {
        if ($memberName -match 'ShapeModel') { return '"model.shm"' }
        if ($memberName -match 'NccModel') { return '"model.ncm"' }
        if ($memberName -match 'Region') { return '"region.hobj"' }
        if ($memberName -match 'Tuple') { return '"data.tup"' }
        if ($memberName -match 'Image') { return '"image.png"' }
        return '"data.dat"'
    }
    if ($typeHint -match 'Jl(Image|Region|XLDCont|XLD|Object|ShapeModel|NCCModel|Measure|HomMat2D|Tuple|Pose|Matrix|Handle)\b') {
        return $paramName
    }
    if ($typeHint -match 'string') { return '"value"' }
    if ($typeHint -match 'bool') { return 'true' }
    if ($typeHint -match 'int') { return '0' }
    if ($typeHint -match 'double') { return '0.0' }
    return $paramName
}

function Get-FlatNameList($raw) {
    $list = New-Object System.Collections.Generic.List[string]
    $stack = New-Object System.Collections.Generic.Stack[object]
    $stack.Push($raw)
    while ($stack.Count -gt 0) {
        $x = $stack.Pop()
        if ($null -eq $x) { continue }
        if ($x -is [string]) { [void]$list.Add($x); continue }
        if ($x -is [System.Collections.IEnumerable]) {
            $tmp = New-Object System.Collections.Generic.List[object]
            foreach ($i in $x) { [void]$tmp.Add($i) }
            for ($k = $tmp.Count - 1; $k -ge 0; $k--) { $stack.Push($tmp[$k]) }
            continue
        }
        [void]$list.Add([string]$x)
    }
    return $list
}

function Build-CallArgs([string]$signature, $paramNamesRaw, $paramDocs, [string]$memberName) {
    $argList = New-Object System.Collections.Generic.List[string]
    $preludes = New-Object System.Collections.Generic.List[string]
    $usedPrelude = @{}
    $nameList = Get-FlatNameList $paramNamesRaw
    foreach ($p in $nameList) {
        if ([string]::IsNullOrWhiteSpace($p)) { continue }
        $hint = Get-ParamTypeHint $signature $p
        if ($hint -match '(^|\s)out\s') {
            $t = 'var'
            if ($hint -match '(Jl\w+|double|int|string|bool|byte\[\])') { $t = $Matches[1] }
            [void]$argList.Add(('out {0} {1}' -f $t, $p))
            continue
        }
        $code = $null
        if ($null -ne $paramDocs -and $paramDocs.ContainsKey($p) -and $paramDocs[$p].Default) {
            $code = Convert-DefaultToCode ([string]$paramDocs[$p].Default) $hint
        }
        if ([string]::IsNullOrWhiteSpace($code)) {
            $code = Get-FallbackSample $p $hint $memberName
            if ($hint -match '(Jl(?:Image|Region|XLDCont|XLD|Object|ShapeModel|NCCModel|Measure|HomMat2D|Tuple|Pose|Matrix|Handle))\b' -and ($code -notmatch '^new ') -and ($code -notmatch '^"')) {
                $typeName = $Matches[1]
                if (-not $usedPrelude.ContainsKey($code)) {
                    [void]$preludes.Add(('{0} {1} = ...;' -f $typeName, $code))
                    $usedPrelude[$code] = $true
                }
            }
        }
        [void]$argList.Add($code)
    }
    $script:LastArgText = [string]::Join(', ', $argList.ToArray())
    $script:LastPreludes = $preludes
    return
}

function Get-UsageExample([string]$className, [string]$memberName, [string]$signature, $paramNames, $paramDocs) {
    $nl = [Environment]::NewLine
    $null = Build-CallArgs $signature $paramNames $paramDocs $memberName
    $argText = $script:LastArgText
    $preludes = $script:LastPreludes
    $pre = ''
    if ($preludes -and $preludes.Count -gt 0) { $pre = [string]::Join($nl, $preludes.ToArray()) + $nl }

    if ($signature -match '\bthis\s*\[') {
        return "${className} obj = ...;${nl}var item = obj[0];"
    }
    if ($signature -match 'implicit operator') {
        return "JlData data = ...;${nl}JlTuple tuple = data;"
    }
    if ($memberName -eq $className) {
        return "${pre}$className obj = new $className($argText);"
    }
    $isProp = ($signature -match '\{\s*get' -or ($signature -match '\bget;' -and $signature -notmatch '\('))
    if ($isProp -or ($signature -notmatch '\(' -and $signature -match '\{')) {
        if ($signature -match '\bset;|\bset \{') {
            return "$className obj = ...;${nl}var value = obj.$memberName;${nl}obj.$memberName = value;"
        }
        return "$className obj = ...;${nl}var value = obj.$memberName;"
    }
    if ($signature -match '\bstatic\b') {
        $call = "$className.$memberName($argText);"
        if ($signature -notmatch '\bvoid\b') { $call = "var result = $call" }
        return "$pre$call"
    }
    if ($signature -match '\bvoid\b') {
        return "${pre}$className obj = ...;${nl}obj.$memberName($argText);"
    }
    return "${pre}$className obj = ...;${nl}var result = obj.$memberName($argText);"
}

function Escape-XmlText([string]$text) {
    if ($null -eq $text) { return '' }
    return ($text -replace '&', '&amp;' -replace '<', '&lt;' -replace '>', '&gt;')
}

function Get-FunctionText([string]$memberName, [string]$existingSummary, [string]$className) {
    $translated = Convert-SummaryToZh $existingSummary
    if ($memberName -eq $className) {
        if ($translated) { return "创建 ${className}：${translated}" }
        return "创建 $className 实例。"
    }
    if ($translated) { return $translated }
    $nameZh = Get-NameZh $memberName
    if ($nameZh -and $nameZh -ne $memberName) { return "${nameZh}。" }
    if ($existingSummary) {
        $clean = [regex]::Replace($existingSummary, '\s*(Modified )?Instance represents:.*$', '').Trim().TrimEnd('.')
        if ($clean) { return "${clean}。" }
    }
    return "${memberName}。"
}

function Build-DocBlock([string]$className, [string]$memberName, [string]$signature, [string]$existingSummary, $paramNames, $paramDocs) {
    $functionText = Escape-XmlText (Get-FunctionText $memberName $existingSummary $className)
    $scene = Get-Scene $memberName $className $signature
    $example = Get-UsageExample $className $memberName $signature $paramNames $paramDocs
    $related = Get-RelatedOperators $memberName
    $out = New-Object System.Collections.Generic.List[string]
    $out.Add("`t/// <remarks>")
    $out.Add("`t///   <para><b>功能说明</b></para>")
    $out.Add("`t///   <para>$functionText</para>")
    if ($scene) {
        $out.Add("`t///   <para><b>典型场景</b></para>")
        $out.Add("`t///   <para>$(Escape-XmlText $scene)</para>")
    }
    $out.Add("`t///   <para><b>调用示例</b></para>")
    $out.Add("`t///   <code>")
    foreach ($codeLine in ($example -split "`r?`n")) {
        if ($codeLine.Length -gt 0) { $out.Add("`t///   $codeLine") }
    }
    $out.Add("`t///   </code>")
    if ($related.Count -gt 0) {
        $uniq = $related | Select-Object -Unique
        $out.Add("`t///   <para><b>相关算子</b></para>")
        $out.Add("`t///   <para>$([string]::Join('、', $uniq))</para>")
    }
    $out.Add("`t/// </remarks>")
    return $out
}

function Test-DocHas([string[]]$lines, [int]$index, [string]$tag) {
    $start = Get-CommentBlockStart $lines $index
    for ($i = $start; $i -lt $index; $i++) {
        if ($lines[$i] -match "<$tag>") { return $true }
    }
    return $false
}

function Test-IsPublicOperatorMember([string]$line, [string]$className) {
    if ($line -notmatch '^\tpublic ') { return $false }
    if ($line -match '^\tpublic (class|enum|struct|delegate|interface) ') { return $false }
    if ($OperatorsOnly -and $className -ne 'JlOperatorSet' -and $line -notmatch 'public static void ') { return $false }
    return $true
}

function Strip-RemarksBlocks([string[]]$lines) {
    $out = New-Object System.Collections.Generic.List[string]
    $inRemarks = $false
    foreach ($line in $lines) {
        if ($line -match '<remarks>') { $inRemarks = $true; continue }
        if ($inRemarks) {
            if ($line -match '</remarks>') { $inRemarks = $false }
            continue
        }
        $out.Add($line)
    }
    return @($out.ToArray())
}

$totalUpdated = 0
foreach ($filePath in $targetFiles) {
    if (-not (Test-Path $filePath)) {
        Write-Host "Skip missing: $(Split-Path $filePath -Leaf)"
        continue
    }
    $lines = [IO.File]::ReadAllLines($filePath)
    if ($Force) { $lines = Strip-RemarksBlocks $lines }
    $className = Get-ClassName $lines
    $out = New-Object System.Collections.Generic.List[string]
    $fileUpdates = 0
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ((Test-IsPublicOperatorMember $line $className) -and -not (Test-HasEditorBrowsableNever $lines $i) -and -not (Test-DocHas $lines $i 'remarks')) {
            $signature = Get-MemberSignature $lines $i
            $memberName = Get-MemberName $signature $className
            $paramNames = @(Get-ParamNames $signature)
            $existingSummary = Get-ExistingSummary $lines $i
            $paramDocs = Get-ParamDocMap $lines $i
            foreach ($docLine in (Build-DocBlock $className $memberName $signature $existingSummary $paramNames $paramDocs)) {
                $out.Add($docLine)
            }
            $fileUpdates++
        }
        $out.Add($line)
    }
    if ($fileUpdates -gt 0) {
        [IO.File]::WriteAllLines($filePath, $out, (New-Object System.Text.UTF8Encoding $false))
        $totalUpdated += $fileUpdates
        Write-Host "Updated $(Split-Path $filePath -Leaf): $fileUpdates member(s)"
    } else {
        Write-Host "No changes: $(Split-Path $filePath -Leaf)"
    }
}

Write-Host "Done. Added documentation blocks for $totalUpdated public member(s)."
