using CommunityToolkit.Mvvm.Input;

namespace RobotVision.WpfHost.Features.CalibrationWizard;

public partial class CalibrationWizardViewModel
{
    public void AddPoint(double pixelX, double pixelY)
    {
        double px = Math.Round(pixelX, 1), py = Math.Round(pixelY, 1);
        if (Mode == WizardMode.Polynomial)
        {
            if (PolynomialImageSpace)
            {
                Message = "棋盘毫米系无需点选：直接点「计算」即可（免示教）";
                return;
            }
            if (_chessboardCorners.Length > 0)
            {
                if (Points.Count >= 2)
                {
                    Message = "多项式标定只需 2 个参考角点（用「清空点」重选）";
                    return;
                }
                var corner = _chessboardCorners[_wizard.NearestPolynomialCornerIndex(
                    _chessboardCorners, (float)pixelX, (float)pixelY)];
                px = Math.Round(corner.X, 2);
                py = Math.Round(corner.Y, 2);
            }
        }

        Points.Add(new CalibPointItem
        {
            Index = Points.Count + 1,
            PixelX = px,
            PixelY = py,
        });
        OnPointsChanged();
        Message = Mode == WizardMode.Polynomial
            ? $"参考点 #{Points.Count}/2: ({px:0.0}, {py:0.0})，请在右表抄录该角点的机器人坐标"
            : $"点 #{Points.Count}: ({px:0.0}, {py:0.0})";
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private void RemoveLastPoint()
    {
        if (Points.Count == 0)
            return;
        Points.RemoveAt(Points.Count - 1);
        OnPointsChanged();
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private void ClearPoints()
    {
        Points.Clear();
        OnPointsChanged();
    }

    private void OnPointsChanged()
    {
        foreach (var item in Points)
            item.Index = Points.IndexOf(item) + 1;
        OnPropertyChanged(nameof(Points));
        ClearPendingResult(Mode);
        Result = "";
        _measuredToolOffset = null;
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private void FillMeasuredOffset()
    {
        if (_measuredToolOffset is null)
        {
            Message = "请先计算（需 ≥2 个带第4轴角的标记点与工位外参或多项式档案）";
            return;
        }
        ToolOffsetDeg = _measuredToolOffset.Value;
        Message = $"已填入实测偏角 δ = {_measuredToolOffset.Value:0.00}°（若与预期差约 180°，请手动加/减 180）";
    }
}
