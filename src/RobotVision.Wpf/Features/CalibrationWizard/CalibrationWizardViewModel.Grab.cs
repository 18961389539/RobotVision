using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core;
using RobotVision.Hosting;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.CalibrationWizard;

public partial class CalibrationWizardViewModel
{
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task GrabAsync()
    {
        this.Commit();
        if (SelectedCamera.Length == 0)
        {
            Message = "请先选择相机";
            return;
        }

        if ((Mode is WizardMode.Extrinsic or WizardMode.Polynomial) && Points.Count > 0 &&
            !_dialogs.ConfirmYesNo("重新取图将清空已录入的参考点。继续？", "重新取图"))
            return;

        var mode = Mode;
        var cols = Cols;
        var rows = Rows;
        var stationId = StationId.Trim();
        var polynomialImageSpace = PolynomialImageSpace;
        var cameraId = SelectedCamera;

        var generation = _pageSession.CaptureGeneration();
        var ct = _pageSession.Token;
        var work = GrabCoreAsync(cameraId, mode, cols, rows, stationId, polynomialImageSpace, generation, ct);
        _pageSession.Track(work);
        await work;
    }

    private async Task GrabCoreAsync(
        string cameraId,
        WizardMode mode,
        int cols,
        int rows,
        string stationId,
        bool polynomialImageSpace,
        int generation,
        CancellationToken ct)
    {
        IsBusy = true;
        try
        {
            Message = $"取图中 · {cameraId} …";
            var result = await Task.Run(
                () => _wizard.GrabFrame(
                    new CalibrationGrabRequest(
                        CalibrationWizardModeMapping.ToHosting(mode), cameraId, cols, rows, stationId, polynomialImageSpace),
                    ct),
                ct);

            if (!_pageSession.IsCurrent(generation) || ct.IsCancellationRequested)
                return;

            if (mode is WizardMode.Extrinsic or WizardMode.Polynomial)
            {
                Points.Clear();
                OnPointsChanged();
            }

            if (mode == WizardMode.Intrinsic)
                _lastChessboardFound = result.ChessboardFound;
            if (mode == WizardMode.Polynomial)
                _chessboardCorners = result.ChessboardCorners.ToArray();

            FrameImage = ImageConverter.ToBitmapSource(result.Display);
            _frameWidth = result.Display.Width;
            _frameHeight = result.Display.Height;
            _lastRawFrame = result.Raw;
            FrameInfo = result.Info;
        }
        catch (OperationCanceledException)
        {
        }
        catch (VisionException vex)
        {
            if (!_pageSession.IsCurrent(generation))
                return;
            var needIntrinsic = mode is WizardMode.Extrinsic
                || (mode is WizardMode.Rotation && !_calibration.HasPolynomial(stationId));
            Message = needIntrinsic
                ? $"{vex.Message}（外参/旋转中心取图前须先完成该相机的内参标定）"
                : vex.Message;
        }
        catch (Exception ex)
        {
            if (_pageSession.IsCurrent(generation))
                Message = $"取图失败: {ex.Message}";
        }
        finally
        {
            if (_pageSession.IsCurrent(generation))
                IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private void CaptureFrame()
    {
        if (_lastRawFrame is null)
        {
            Message = "请先取图";
            return;
        }
        if (!_lastChessboardFound)
        {
            Message = "当前帧未检测到棋盘，不能加入采集";
            return;
        }

        var index = ++CollectedFrames;
        var path = Path.Combine(TempFolder(), $"frame_{index:D3}.png");
        _wizard.SaveFramePng(_lastRawFrame, path);
        Message = $"已加入第 {index} 帧（建议 ≥{_wizard.IntrinsicMinImageCount} 帧）";
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private void ClearIntrinsic()
    {
        try
        {
            if (Directory.Exists(_intrinsicTempFolder))
                Directory.Delete(_intrinsicTempFolder, true);
        }
        catch (IOException) { /* 下次换新目录 */ }
        _intrinsicTempFolder = "";
        CollectedFrames = 0;
        ClearPendingResult(WizardMode.Intrinsic);
        Result = "";
        Message = "已清空采集";
    }
}
