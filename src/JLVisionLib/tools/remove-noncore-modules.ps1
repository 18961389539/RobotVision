$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = Join-Path $root 'JLVisionLib.Runtime'

$deleteFiles = @(
    # Communication & device I/O
    'JlSocket.cs', 'JlSerial.cs', 'JlFile.cs', 'JlIODevice.cs', 'JlIOChannel.cs', 'JlFramegrabber.cs',
    # Concurrency / synchronization
    'JlMutex.cs', 'JlBarrier.cs', 'JlCondition.cs', 'JlEvent.cs', 'JlMessage.cs', 'JlMessageQueue.cs',
    # Deep learning
    'JlDlModel.cs', 'JlDlDevice.cs', 'JlComputeDevice.cs', 'JlDlLayer.cs',
    'JlDlModelOcr.cs', 'JlDlModelCounting.cs', 'JlDlTransform.cs', 'JlDlTransformPipeline.cs',
    'JlDlPrune.cs', 'JlDeepMatching3D.cs',
    # Dev / debug helpers
    'JlDevDisposeHelper.cs', 'JlDevThread.cs', 'JlDevThreadContext.cs', 'JlDevParamGuard.cs', 'JlInfo.cs'
)
foreach ($name in $deleteFiles) {
    $path = Join-Path $runtime $name
    if (Test-Path $path) { Remove-Item $path -Force; Write-Host "Deleted $name" }
}

$stripTargets = @(
    'JlOperatorSet.cs', 'JlImage.cs', 'JlRegion.cs', 'JlXLD.cs', 'JlSerializedItem.cs'
) | ForEach-Object { Join-Path $runtime $_ }

& (Join-Path $PSScriptRoot 'strip-methods.ps1') -Files $stripTargets

function Remove-JlNativeApiDevThreadApi([string]$path) {
    $text = [IO.File]::ReadAllText($path)
    $text = [regex]::Replace($text, '(?ms)\r?\n\t\[EditorBrowsable\(EditorBrowsableState\.Never\)\]\r?\n\tpublic delegate IntPtr JlDevThreadInternalCallback\(IntPtr devThread\);\r?\n', "`r`n")
    foreach ($method in @(
        'HXCreateHThreadContext\(out IntPtr context\)',
        'HXClearHThreadContext\(IntPtr context\)',
        'HXCreateHThread\(IntPtr contextHandle, out IntPtr threadHandle\)',
        'HXClearHThread\(IntPtr threadHandle\)',
        'HXExitHThread\(IntPtr threadHandle\)',
        'HXStartHThreadDotNet\(IntPtr threadHandle, JlDevThreadInternalCallback proc, IntPtr data, out IntPtr threadId\)',
        'HXPrepareDirectCall\(IntPtr threadHandle\)',
        'HXJoinHThread\(IntPtr threadId\)',
        'HXThreadLockLocalVar\(IntPtr threadHandle, out IntPtr referenceCount\)',
        'HXThreadUnlockLocalVar\(IntPtr threadHandle\)',
        'HXThreadLockGlobalVar\(IntPtr threadHandle\)',
        'HXThreadUnlockGlobalVar\(IntPtr threadHandle\)'
    )) {
        $text = [regex]::Replace($text, "(?ms)\r?\n\t\[DllImport\(`"JLVisionCore`", CallingConvention = CallingConvention\.Cdecl\)\]\r?\n\tinternal static extern int $method;\r?\n", "`r`n")
    }
    [IO.File]::WriteAllText($path, $text)
    Write-Host 'Updated JlNativeApi.cs (dev thread APIs removed)'
}

Remove-JlNativeApiDevThreadApi (Join-Path $runtime 'JlNativeApi.cs')

Write-Host 'Done.'
