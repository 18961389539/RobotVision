using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Hosting;

namespace RobotVision.WpfHost.Features.CalibrationWizard;

public partial class CalibrationWizardViewModel
{
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ComputeAsync()
    {
        if (!TryCreateComputeRequest(out var request, out var error))
        {
            Message = error!;
            return;
        }

        var generation = _pageSession.CaptureGeneration();
        var ct = _pageSession.Token;
        var work = ComputeCoreAsync(request, generation, ct);
        _pageSession.Track(work);
        await work;
    }

    private bool TryCreateComputeRequest(out CalibrationComputeRequest request, out string? error)
    {
        request = null!;
        error = null;
        this.Commit();

        var cameraId = SelectedCamera;
        if (string.IsNullOrWhiteSpace(cameraId))
        {
            error = "请先选择相机";
            return false;
        }

        var stationId = StationId.Trim();
        var points = Points.Select(p => new CalibrationPointSnapshot(
            p.PixelX, p.PixelY, p.RobotX, p.RobotY, p.RobotEntered, p.RobotRzDeg)).ToArray();

        switch (Mode)
        {
            case WizardMode.Intrinsic:
            {
                var framePaths = Directory.GetFiles(TempFolder(), "*.png");
                if (framePaths.Length < _wizard.IntrinsicMinImageCount)
                {
                    error = $"内参标定至少需要 {_wizard.IntrinsicMinImageCount} 张有效图（当前 {framePaths.Length} 张）";
                    return false;
                }

                request = new CalibrationComputeRequest(
                    CalibrationWizardModeMapping.ToHosting(Mode), cameraId, stationId, Cols, Rows, SquareMm, PolynomialOrder, PolynomialImageSpace,
                    ToolOffsetDeg, points, [], 0, 0, framePaths, 0, 0, false);
                return true;
            }
            case WizardMode.Extrinsic:
            {
                try
                {
                    _calibration.RequireIntrinsic(cameraId);
                }
                catch (VisionException vex)
                {
                    error = vex.Message;
                    return false;
                }

                var (width, height) = ResolveMappingImageSize(stationId, cameraId);
                request = new CalibrationComputeRequest(
                    CalibrationWizardModeMapping.ToHosting(Mode), cameraId, stationId, Cols, Rows, SquareMm, PolynomialOrder, PolynomialImageSpace,
                    ToolOffsetDeg, points, [], 0, 0, [], width, height, false);
                return true;
            }
            case WizardMode.Polynomial:
            {
                if (_chessboardCorners.Length == 0)
                {
                    error = "请先取图并检测到棋盘角点";
                    return false;
                }

                if (_frameWidth <= 0 || _frameHeight <= 0)
                {
                    error = "内部错误：当前帧不可用，请重新取图";
                    return false;
                }

                if (!PolynomialImageSpace &&
                    (Points.Count < 2 || Points.Any(p => !p.RobotEntered)))
                {
                    error = "请先点选 2 个参考角点并抄录其机器人坐标（原点 0,0 请勾选「已抄录」；或改用「棋盘毫米系」免示教）";
                    return false;
                }

                request = new CalibrationComputeRequest(
                    CalibrationWizardModeMapping.ToHosting(Mode), cameraId, stationId, Cols, Rows, SquareMm, PolynomialOrder, PolynomialImageSpace,
                    ToolOffsetDeg, points, _chessboardCorners,
                    _frameWidth, _frameHeight, [], 0, 0, false);
                return true;
            }
            case WizardMode.Rotation:
            {
                var rotationHasPolynomial = stationId.Length > 0 && _calibration.HasPolynomial(stationId);
                int rotWidth, rotHeight;
                try
                {
                    (rotWidth, rotHeight) = ResolveMappingImageSize(stationId, cameraId);
                }
                catch (VisionException vex)
                {
                    error = vex.Message;
                    return false;
                }

                request = new CalibrationComputeRequest(
                    CalibrationWizardModeMapping.ToHosting(Mode), cameraId, stationId, Cols, Rows, SquareMm, PolynomialOrder, PolynomialImageSpace,
                    ToolOffsetDeg, points, [], 0, 0, [], rotWidth, rotHeight, rotationHasPolynomial);
                return true;
            }
            default:
                error = "未知标定模式";
                return false;
        }
    }

    private (int Width, int Height) ResolveMappingImageSize(string stationId, string cameraId)
    {
        if (stationId.Length > 0 && _calibration.HasPolynomial(stationId))
        {
            var poly = _calibration.PolynomialProfiles.First(p =>
                string.Equals(p.StationId, stationId, StringComparison.OrdinalIgnoreCase));
            return (poly.Width, poly.Height);
        }

        _calibration.RequireIntrinsic(cameraId);
        var intrinsic = GetIntrinsic(cameraId);
        return (intrinsic?.Width ?? 0, intrinsic?.Height ?? 0);
    }

    private void ClearPendingResult(WizardMode mode)
    {
        switch (mode)
        {
            case WizardMode.Intrinsic:
                _pendingIntrinsic = null;
                break;
            case WizardMode.Extrinsic:
                _pendingExtrinsic = null;
                break;
            case WizardMode.Polynomial:
                _pendingPolynomial = null;
                break;
            case WizardMode.Rotation:
                _pendingRotation = null;
                _measuredToolOffset = null;
                break;
        }

        if (_pendingResultMode == mode)
            _pendingResultMode = null;
    }

    private void ClearAllPendingResults()
    {
        _pendingIntrinsic = null;
        _pendingExtrinsic = null;
        _pendingPolynomial = null;
        _pendingRotation = null;
        _measuredToolOffset = null;
        _pendingResultMode = null;
    }

    private async Task ComputeCoreAsync(CalibrationComputeRequest request, int generation, CancellationToken ct)
    {
        IsBusy = true;
        try
        {
            if (!_pageSession.IsCurrent(generation))
                return;

            ClearPendingResult(CalibrationWizardModeMapping.ToWizard(request.Mode));
            Message = "计算中...";
            Result = "";

            var computed = await Task.Run(() => _wizard.Compute(request), ct);
            if (!_pageSession.IsCurrent(generation) || ct.IsCancellationRequested)
                return;

            switch (request.Mode)
            {
                case CalibrationWizardMode.Intrinsic:
                    _pendingIntrinsic = computed.Intrinsic
                        ?? throw new VisionException(VisionErrorCode.InternalError, "内参标定未返回结果");
                    Result = Format(_pendingIntrinsic);
                    break;
                case CalibrationWizardMode.Extrinsic:
                    _pendingExtrinsic = computed.Extrinsic
                        ?? throw new VisionException(VisionErrorCode.InternalError, "外参标定未返回结果");
                    Result = Format(_pendingExtrinsic);
                    break;
                case CalibrationWizardMode.Polynomial:
                    _pendingPolynomial = computed.Polynomial
                        ?? throw new VisionException(VisionErrorCode.InternalError, "多项式标定未返回结果");
                    Result = Format(_pendingPolynomial);
                    break;
                case CalibrationWizardMode.Rotation:
                {
                    _pendingRotation = computed.Rotation
                        ?? throw new VisionException(VisionErrorCode.InternalError, "旋转中心标定未返回结果");
                    Result = Format(_pendingRotation);
                    _measuredToolOffset = null;

                    var paired = request.Points.Where(p => p.RobotRzDeg.HasValue).ToArray();
                    var rotationHasMapping = request.RotationHasPolynomial ||
                        _calibration.ExtrinsicProfiles.Any(e =>
                            string.Equals(e.StationId, request.StationId, StringComparison.OrdinalIgnoreCase));
                    var pairedPoints = paired.Select(p => new Point2f((float)p.PixelX, (float)p.PixelY)).ToArray();
                    var pairedAngles = paired.Select(p => p.RobotRzDeg!.Value).ToArray();

                    if (!rotationHasMapping)
                    {
                        Result += paired.Length >= 2
                            ? "\n提示: 工位无外参/多项式档案，跳过方向自检与偏角实测（建议先标映射，再回来验证）"
                            : "\n提示: 在点表\"第4轴角\"列填写各点角度（≥2 个 + 工位外参或多项式）可实测工具零位偏角；≥3 个可同时做旋转方向自检";
                        break;
                    }

                    if (paired.Length >= 3)
                    {
                        _calibration.VerifyRotationDirection(request.StationId, _pendingRotation,
                            pairedPoints, pairedAngles);
                        Result += "\n方向自检通过: 第 4 轴正方向与图像旋转方向一致";
                    }
                    else if (paired.Length > 0)
                    {
                        Result += "\n提示: ≥3 个带角度标记点可同时做旋转方向自检";
                    }

                    if (paired.Length >= 2)
                    {
                        var (offset, spread) = _calibration.ComputeToolOffsetDeg(
                            request.StationId, _pendingRotation, pairedPoints, pairedAngles);
                        _measuredToolOffset = Math.Round(offset, 2);
                        Result += $"\n实测工具零位偏角 δ ≈ {offset:0.00}°（离散度 {spread:0.00}°）——点「填入实测偏角」自动填入";
                        if (spread > 5.0)
                            Result += "\n警告: 偏角离散度偏大，标记提取噪声或轴心误差影响实测值，建议核对";
                        Result += "\n提示: 若实测值与预期差约 180°，说明标记取在工具另一端，请手动加/减 180";
                    }
                    break;
                }
            }
            if (!_pageSession.IsCurrent(generation) || ct.IsCancellationRequested)
                return;
            _pendingResultMode = CalibrationWizardModeMapping.ToWizard(request.Mode);
            Message = "计算完成，确认指标后保存";
        }
        catch (OperationCanceledException)
        {
        }
        catch (VisionException vex)
        {
            if (!_pageSession.IsCurrent(generation))
                return;
            ClearPendingResult(CalibrationWizardModeMapping.ToWizard(request.Mode));
            Result = "";
            Message = $"标定失败: {vex.Message}";
        }
        catch (Exception ex)
        {
            if (!_pageSession.IsCurrent(generation))
                return;
            ClearPendingResult(CalibrationWizardModeMapping.ToWizard(request.Mode));
            Result = "";
            Message = $"标定失败: {ex.Message}";
        }
        finally
        {
            if (_pageSession.IsCurrent(generation))
                IsBusy = false;
        }
    }
}
