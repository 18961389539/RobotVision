$ErrorActionPreference = 'Stop'
$runtime = Join-Path (Split-Path $PSScriptRoot -Parent) 'JLVisionLib.Runtime'

Get-ChildItem $runtime -Filter '*.cs' | ForEach-Object {
    $text = [IO.File]::ReadAllText($_.FullName)
    $orig = $text

    $text = $text -replace '(?m)^\t\tbyte\[\] data = JlSerializationBuffer\.LoadBytes\(proc, (\d+), err\);\t*int err = JlNativeApi\.CallProcedure\(proc\);\r?\n\t\tJlNativeApi\.PostCall\(proc, err\);\t*int err = JlNativeApi\.CallProcedure\(proc\);\r?\n\t\tGC\.KeepAlive\(this\);\t*int err = JlNativeApi\.CallProcedure\(proc\);\r?\n\t\treturn data;', "`t`tbyte[] data = JlSerializationBuffer.LoadBytes(proc, `$1, err);`r`n`t`tJlNativeApi.PostCall(proc, err);`r`n`t`tGC.KeepAlive(this);`r`n`t`treturn data;"

    $text = [regex]::Replace($text, '(?ms)(\t\tint err = JlNativeApi\.CallProcedure\(proc\);\r?\n(?:\t\tUnpinTuple\(\);\r?\n)?)\t\terr = JlSerializationBuffer\.LoadNew\(proc, (\d+), err, out JlSerializationBuffer obj\);\r?\n\t\tJlNativeApi\.PostCall\(proc, err\);\r?\n\t\tGC\.KeepAlive\(this\);\r?\n\t\treturn obj;', {
        param($m)
        $prefix = $m.Groups[1].Value
        $par = $m.Groups[2].Value
        return "${prefix}		byte[] data = JlSerializationBuffer.LoadBytes(proc, $par, err);${prefix}		JlNativeApi.PostCall(proc, err);${prefix}		GC.KeepAlive(this);${prefix}		return data;"
    })

    $text = [regex]::Replace($text, '(?ms)(public (?:new )?(?:static )?void Deserialize\w+\(byte\[\] serializedItemHandle\)\r?\n\t\{)\r?\n\t\tDispose\(\);', '$1$2		using JlSerializationBuffer buffer = new JlSerializationBuffer(serializedItemHandle);$2		Dispose();')

    $text = $text.Replace('public new void DeserializeXld(JlSerializationBuffer serializedItemHandle)', 'public new void DeserializeXld(byte[] serializedItemHandle)')
    $text = $text.Replace('public new JlSerializationBuffer SerializeXld()', 'public new byte[] SerializeXld()')
    $text = $text.Replace('public static JlTuple DeserializeTuple(JlSerializationBuffer serializedItemHandle)', 'public static JlTuple DeserializeTuple(byte[] serializedItemHandle)')
    $text = $text.Replace('public static void DeserializeFftOptimizationData(JlSerializationBuffer serializedItemHandle)', 'public static void DeserializeFftOptimizationData(byte[] serializedItemHandle)')
    $text = $text.Replace('public static JlSerializationBuffer SerializeFftOptimizationData()', 'public static byte[] SerializeFftOptimizationData()')

    if ($text -ne $orig) {
        [IO.File]::WriteAllText($_.FullName, $text)
        Write-Host "Fixed $(Split-Path $_.FullName -Leaf)"
    }
}

& (Join-Path $PSScriptRoot 'strip-methods.ps1') -Files @(
    (Join-Path $runtime 'JlCamPar.cs'),
    (Join-Path $runtime 'JlPose.cs'),
    (Join-Path $runtime 'JlHandle.cs'),
    (Join-Path $runtime 'JlHomMat2D.cs'),
    (Join-Path $runtime 'JlHomMat3D.cs'),
    (Join-Path $runtime 'JlQuaternion.cs'),
    (Join-Path $runtime 'JlDualQuaternion.cs'),
    (Join-Path $runtime 'JlMisc.cs')
)

Write-Host 'Serialization fixes done.'
