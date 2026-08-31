using System.Collections.Concurrent;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Infrastructure;

namespace RobotVision.Infrastructure.Calibration;

/// <summary>内参档案与 OpenCV Remap 生命周期（读写锁保护热加载）。</summary>
internal sealed class IntrinsicCalibrationService : IDisposable
{
    private sealed record IntrinsicState(IntrinsicProfile Profile, Mat MapX, Mat MapY);

    private readonly ConcurrentDictionary<string, IntrinsicState> _intrinsics = new(StringComparer.OrdinalIgnoreCase);
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Action<string> _warn;

    public IntrinsicCalibrationService(Action<string> warn) => _warn = warn;

    public int Count => _intrinsics.Count;

    public IReadOnlyList<IntrinsicProfile> Profiles =>
        _intrinsics.Values.Select(s => s.Profile).OrderBy(p => p.CameraId, StringComparer.OrdinalIgnoreCase).ToList();

    public bool IsCalibrated(string cameraId) => _intrinsics.ContainsKey(cameraId);

    public void RequireIntrinsic(string cameraId)
    {
        if (!_intrinsics.ContainsKey(cameraId))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"相机未做内参标定: {cameraId}（外参/旋转中心标定前必须先完成内参标定）");
    }

    public void Load(IntrinsicProfile profile)
    {
        Validate(profile);

        using var cameraMatrix = ToMat(profile.CameraMatrix, 3, 3);
        using var distCoeffs = ToMat(profile.DistCoeffs, 1, profile.DistCoeffs.Length);
        using var noRotation = new Mat();

        var mapX = new Mat();
        var mapY = new Mat();
        try
        {
            Cv2.InitUndistortRectifyMap(
                cameraMatrix, distCoeffs, noRotation, cameraMatrix,
                new Size(profile.Width, profile.Height), MatType.CV_32FC1, mapX, mapY);
        }
        catch
        {
            mapX.Dispose();
            mapY.Dispose();
            throw;
        }

        _lock.EnterWriteLock();
        try
        {
            if (_intrinsics.TryRemove(profile.CameraId, out var old))
            {
                old.MapX.Dispose();
                old.MapY.Dispose();
            }
            _intrinsics[profile.CameraId] = new IntrinsicState(profile, mapX, mapY);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        if (Assess(profile) == CalibrationQuality.Poor)
            _warn($"内参 {profile.CameraId} 质量超标: RMS {profile.Rms:0.000}px（>{CalibrationConstants.IntrinsicRmsFair:0.0} 可用上限），建议重新标定");
    }

    public bool Delete(string cameraId)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_intrinsics.TryRemove(cameraId, out var old))
            {
                old.MapX.Dispose();
                old.MapY.Dispose();
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return true;
    }

    public VisionImage Undistort(string cameraId, VisionImage src)
    {
        using var mat = VisionImageCv.AsMat(src);
        Mat? undistorted = Undistort(cameraId, mat);
        try
        {
            var image = VisionImageCv.Adopt(undistorted);
            undistorted = null;
            return image;
        }
        finally
        {
            undistorted?.Dispose();
        }
    }

    public Mat Undistort(string cameraId, Mat src)
    {
        _lock.EnterReadLock();
        try
        {
            if (!_intrinsics.TryGetValue(cameraId, out var state))
                throw new VisionException(VisionErrorCode.NotCalibrated, $"相机未做内参标定: {cameraId}");

            if (src.Width != state.Profile.Width || src.Height != state.Profile.Height)
                throw new VisionException(
                    VisionErrorCode.NotCalibrated,
                    $"图像分辨率 {src.Width}x{src.Height} 与内参档案 {state.Profile.Width}x{state.Profile.Height} 不一致，请重新标定");

            var dst = new Mat();
            Cv2.Remap(src, dst, state.MapX, state.MapY, InterpolationFlags.Linear);
            return dst;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool TryGetProfile(string cameraId, out IntrinsicProfile profile)
    {
        if (_intrinsics.TryGetValue(cameraId, out var state))
        {
            profile = state.Profile;
            return true;
        }
        profile = null!;
        return false;
    }

    public void VerifyResolutionConsistency(string cameraId, int width, int height, string profileKind)
    {
        if (width <= 0 || height <= 0)
            return;
        if (_intrinsics.TryGetValue(cameraId, out var state) &&
            (state.Profile.Width != width || state.Profile.Height != height))
            throw new VisionException(VisionErrorCode.NotCalibrated,
                $"{profileKind}标定分辨率 {width}x{height} 与相机 {cameraId} 当前内参 {state.Profile.Width}x{state.Profile.Height} 不一致" +
                "（换相机/改分辨率后需重新标定外参/旋转中心）");
    }

    public static void Validate(IntrinsicProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.CameraId))
            throw new VisionException(VisionErrorCode.InternalError, "内参 CameraId 为空（空串 Id 会导致档案互相覆盖）");
        if (profile.CameraMatrix is not { Length: 9 })
            throw new VisionException(VisionErrorCode.InternalError,
                $"内参 CameraMatrix 必须为 9 元素，当前 {profile.CameraMatrix?.Length ?? 0}");
        if (profile.CameraMatrix.Any(v => !double.IsFinite(v)) || profile.DistCoeffs.Any(v => !double.IsFinite(v)))
            throw new VisionException(VisionErrorCode.InternalError,
                $"内参 {profile.CameraId} 的 CameraMatrix/DistCoeffs 含非有限值（NaN/Infinity），档案已损坏");
        if (profile.CameraMatrix[0] <= 0 || profile.CameraMatrix[4] <= 0)
            throw new VisionException(VisionErrorCode.InternalError,
                $"内参 {profile.CameraId} 的焦距非法: fx={profile.CameraMatrix[0]}, fy={profile.CameraMatrix[4]}（必须为正）");
        var marginX = profile.Width * 0.1;
        var marginY = profile.Height * 0.1;
        if (profile.CameraMatrix[2] < -marginX || profile.CameraMatrix[2] > profile.Width + marginX ||
            profile.CameraMatrix[5] < -marginY || profile.CameraMatrix[5] > profile.Height + marginY)
            throw new VisionException(VisionErrorCode.InternalError,
                $"内参 {profile.CameraId} 的主点越界: cx={profile.CameraMatrix[2]}, cy={profile.CameraMatrix[5]} " +
                $"（分辨率 {profile.Width}x{profile.Height}，允许范围含 10% 余量）");
        if (profile.Width <= 0 || profile.Height <= 0)
            throw new VisionException(VisionErrorCode.InternalError, $"内参分辨率非法: {profile.Width}x{profile.Height}");
        if (profile.DistCoeffs.Length > 14)
            throw new VisionException(VisionErrorCode.InternalError, $"内参畸变系数长度非法: {profile.DistCoeffs.Length}");
    }

    public static CalibrationQuality Assess(IntrinsicProfile p) =>
        p.Rms <= CalibrationConstants.IntrinsicRmsGood ? CalibrationQuality.Good
        : p.Rms <= CalibrationConstants.IntrinsicRmsFair ? CalibrationQuality.Fair
        : CalibrationQuality.Poor;

    public void Dispose()
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var state in _intrinsics.Values)
            {
                state.MapX.Dispose();
                state.MapY.Dispose();
            }
            _intrinsics.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        _lock.Dispose();
    }

    private static Mat ToMat(double[] values, int rows, int cols)
    {
        var mat = new Mat(rows, cols, MatType.CV_64F);
        var k = 0;
        for (var i = 0; i < rows; i++)
            for (var j = 0; j < cols; j++)
                mat.Set(i, j, values[k++]);
        return mat;
    }
}
