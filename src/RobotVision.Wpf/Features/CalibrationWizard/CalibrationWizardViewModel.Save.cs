using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core;
using RobotVision.Core.IO;
using RobotVision.Core.Models;
using RobotVision.Hosting;

namespace RobotVision.WpfHost.Features.CalibrationWizard;

public partial class CalibrationWizardViewModel
{
    [RelayCommand(CanExecute = nameof(CanOperate))]
    private void Save()
    {
        this.Commit();
        if (_pendingResultMode is { } pendingMode && pendingMode != Mode)
        {
            Message = "标定类型已变更，请按当前类型重新计算后再保存";
            return;
        }

        try
        {
            switch (Mode)
            {
                case WizardMode.Intrinsic:
                    if (_pendingIntrinsic is null)
                    {
                        Message = "请先计算";
                        return;
                    }
                    if (!ConfirmOverwrite($"{_pendingIntrinsic.CameraId}.intrinsic.json"))
                        return;
                    var intrinsicPath = WriteProfile(_pendingIntrinsic, $"{_pendingIntrinsic.CameraId}.intrinsic.json");
                    _calibration.LoadIntrinsic(_pendingIntrinsic);
                    Message = $"已保存并加载: {intrinsicPath}";
                    break;

                case WizardMode.Extrinsic:
                    if (_pendingExtrinsic is null)
                    {
                        Message = "请先计算";
                        return;
                    }
                    if (StationId.Trim().Length == 0)
                    {
                        Message = "工位 Id 不能为空";
                        return;
                    }
                    if (!ConfirmOverwrite($"{StationId.Trim()}.extrinsic.json"))
                        return;
                    if (_calibration.HasPolynomial(StationId.Trim()) &&
                        !_dialogs.ConfirmYesNo("该工位已有多项式档案，生产将优先使用多项式（本外参会被忽略）。仍要保存外参？",
                            "双档案并存"))
                        return;
                    // 保存时取 UI 当前值（计算后仍可调整安装模式/位姿/平面 Z）
                    _pendingExtrinsic = _pendingExtrinsic with
                    {
                        MountType = SelectedMount,
                        ComposeMode = SelectedMount == CameraMountType.OnArm ? SelectedCompose : PoseComposeMode.Check,
                        TeachTcpX = TeachTcpX,
                        TeachTcpY = TeachTcpY,
                        TeachRzDeg = TeachRzDeg,
                        // 显式标志而非 (0,0,0) 哨兵：OnArm 即视为已记录（拍照点恰为原点也不误判）
                        HasTeachPose = SelectedMount == CameraMountType.OnArm,
                        CalibrationPlaneZ = CalibrationPlaneZ,
                    };
                    var extrinsicPath = WriteProfile(_pendingExtrinsic, $"{StationId.Trim()}.extrinsic.json");
                    _calibration.LoadExtrinsic(_pendingExtrinsic);
                    Result = Format(_pendingExtrinsic); // 与最终保存值同步（计算后改过安装模式/位姿时）
                    Message = $"已保存并加载: {extrinsicPath}"
                        + (SelectedMount == CameraMountType.OnArm
                            ? $"（OnArm: 拍照位姿 {TeachTcpX:0.000}/{TeachTcpY:0.000} RZ {TeachRzDeg:0.0}° 已记录，生产拍照须与此一致）"
                            : "");
                    break;

                case WizardMode.Polynomial:
                    if (_pendingPolynomial is null)
                    {
                        Message = "请先计算";
                        return;
                    }
                    if (StationId.Trim().Length == 0)
                    {
                        Message = "工位 Id 不能为空";
                        return;
                    }
                    if (!ConfirmOverwrite($"{StationId.Trim()}.polynomial.json"))
                        return;
                    if (_calibration.HasExtrinsic(StationId.Trim()) &&
                        !_dialogs.ConfirmYesNo("该工位已有外参档案，保存多项式后生产将走多项式（外参被忽略）。继续？",
                            "双档案并存"))
                        return;
                    // 保存时取 UI 当前值（坐标空间/安装模式/位姿合成模式/平面 Z 可在计算后调整）。
                    // Image 毫米系无机器人系概念：MountType 强制 Fixed、位姿不记录
                    var polyImageSpace = PolynomialImageSpace ||
                                         string.Equals(SelectedSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase);
                    _pendingPolynomial = _pendingPolynomial with
                    {
                        CoordinateSpace = polyImageSpace ? PolynomialCoordinateSpace.Image : PolynomialCoordinateSpace.Robot,
                        MountType = polyImageSpace ? CameraMountType.Fixed : SelectedMount,
                        ComposeMode = polyImageSpace ? PoseComposeMode.Check
                            : SelectedMount == CameraMountType.OnArm ? SelectedCompose : PoseComposeMode.Check,
                        TeachTcpX = polyImageSpace ? 0 : TeachTcpX,
                        TeachTcpY = polyImageSpace ? 0 : TeachTcpY,
                        TeachRzDeg = polyImageSpace ? 0 : TeachRzDeg,
                        HasTeachPose = !polyImageSpace && SelectedMount == CameraMountType.OnArm,
                        CalibrationPlaneZ = CalibrationPlaneZ,
                    };
                    var polyPath = WriteProfile(_pendingPolynomial, $"{StationId.Trim()}.polynomial.json");
                    _calibration.LoadPolynomial(_pendingPolynomial);
                    Result = Format(_pendingPolynomial);
                    Message = $"已保存并加载: {polyPath}（该工位走单图模式：推理直接用原图，无需内参/外参档案）"
                        + (SelectedMount == CameraMountType.OnArm && SelectedCompose == PoseComposeMode.Translate
                            ? "（Translate: 换拍照点不重标，TRIGGER 上报位姿自动合成）"
                            : "");
                    break;

                case WizardMode.Rotation:
                    if (_pendingRotation is null)
                    {
                        Message = "请先计算";
                        return;
                    }
                    if (!ConfirmOverwrite($"{StationId.Trim()}.rotation.json"))
                        return;
                    _pendingRotation = _pendingRotation with { ToolOffsetDeg = ToolOffsetDeg };
                    var rotationPath = WriteProfile(_pendingRotation, $"{StationId.Trim()}.rotation.json");
                    _calibration.LoadRotationCenter(_pendingRotation);
                    Result = Format(_pendingRotation); // 与最终保存值同步
                    Message = $"已保存并加载: {rotationPath}"
                        + (Math.Abs(ToolOffsetDeg) > 1e-9
                            ? $"（含工具零位偏角 {ToolOffsetDeg:0.0}°）"
                            : "");
                    break;
            }
        }
        catch (Exception ex)
        {
            Message = $"保存失败: {ex.Message}";
        }
    }

    private bool ConfirmOverwrite(string fileName)
    {
        var path = Path.Combine(_calibrationFolder, fileName);
        if (!File.Exists(path))
            return true;
        return _dialogs.ConfirmYesNo($"档案 {fileName} 已存在，覆盖保存？", "覆盖确认");
    }

    /// <summary>查相机已加载的内参档案（外参/旋转中心标定需记录同分辨率，供一致性校验）。</summary>
    private IntrinsicProfile? GetIntrinsic(string cameraId) =>
        _calibration.IntrinsicProfiles.FirstOrDefault(
            p => string.Equals(p.CameraId, cameraId, StringComparison.OrdinalIgnoreCase));

    private string WriteProfile<T>(T profile, string fileName)
    {
        Directory.CreateDirectory(_calibrationFolder);
        var path = Path.Combine(_calibrationFolder, fileName);
        // 原子落盘：标定档案是产线关键资产，写一半崩溃不得留下截断 JSON
        AtomicFile.WriteAllText(path,
            JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private string Format(IntrinsicProfile p)
    {
        var k = p.CameraMatrix;
        var text = $"重投影 RMS: {p.Rms:0.000} px（≤0.3 优秀，≤0.5 可用）\n" +
                   $"fx={k[0]:0.0} fy={k[4]:0.0} cx={k[2]:0.0} cy={k[5]:0.0} · {p.Width}×{p.Height} · 有效图 {p.ImageCount} 张";
        if (p.ImageCount > 0 && p.ImageCount < _wizard.IntrinsicRecommendedImageCount)
            text += $"\n提示: 有效图仅 {p.ImageCount} 张，建议 ≥{_wizard.IntrinsicRecommendedImageCount} 张（覆盖四角、姿态多样）";
        if (p.PerImageRms is { Count: > 0 })
            text += $"\n单图 RMS: {p.PerImageRms.Min():0.000} ~ {p.PerImageRms.Max():0.000} px（最大者为疑似坏图）";
        if (p.Rms > 0.5)
            text += "\n警告: RMS 偏大，建议重拍（覆盖四角、姿态多样、对焦清晰）";
        return text;
    }

    private static string Format(ExtrinsicProfile p) =>
        $"RMS 残差: {p.Rms:0.0000} · 最大残差: {p.MaxResidual:0.0000}（机器人单位）" +
        (p.LeaveOneOutMax > 0
            ? $"\n留一最大误差: {p.LeaveOneOutMax:0.0000}（偏大说明存在抄错的点对）"
            : "") +
        (p.MaxResidual > 0.1 ? "\n警告: 残差偏大，请核对点对（像素点与机器人点须一一对应）" : "") +
        (p.MountType == CameraMountType.OnArm
            ? $"\n安装模式: OnArm · {p.ComposeMode}（档案仅在标定拍照位姿 {p.TeachTcpX:0.000}/{p.TeachTcpY:0.000} RZ {p.TeachRzDeg:0.0}° 下有效）"
            : "") +
        (p.CalibrationPlaneZ != 0 ? $"\n标定平面 Z: {p.CalibrationPlaneZ:0.000}" : "");

    private string Format(PolynomialProfile p) =>
        $"多项式阶数: {p.Order}（{p.CoefficientCount} 系数/轴） · 网格点 {p.PointCount}\n" +
        $"输出坐标: {(string.Equals(p.CoordinateSpace, PolynomialCoordinateSpace.Image, StringComparison.OrdinalIgnoreCase)
            ? "棋盘平面毫米系（免示教）" : "机器人系")}\n" +
        $"拟合残差 RMS: {p.Rms:0.0000} · 最大 {p.MaxResidual:0.0000}（mm，参考 ≤0.1 优秀 / ≤0.5 可用）" +
        (p.MaxResidual > _calibration.ExtrinsicResidualFair ? "\n警告: 残差偏大，请重拍（棋盘放平、正对镜头）或核对参数" : "") +
        (string.Equals(p.CoordinateSpace, PolynomialCoordinateSpace.Robot, StringComparison.OrdinalIgnoreCase)
            ? p.MountType == CameraMountType.OnArm
                ? $"\n安装模式: OnArm · {p.ComposeMode}" +
                  (p.HasTeachPose
                      ? $"（标定拍照位姿 {p.TeachTcpX:0.000}/{p.TeachTcpY:0.000} RZ {p.TeachRzDeg:0.0}°）"
                      : "（未记录拍照位姿）")
                : "\n安装模式: Fixed（固定机架）"
            : "");

    private static string Format(RotationCenterProfile p)
    {
        var text = $"轴心像素坐标: ({p.Cx:0.00}, {p.Cy:0.00}) · 半径 {p.RadiusPx:0.00} px\n" +
                   $"半径残差 RMS: {p.Rms:0.000} px（≤0.3 优秀，≤0.5 可用）";
        if (p.PointCount >= 5)
            text += $"\n椭圆长短轴比: {p.AxisRatio:0.000}（1=正圆）";
        if (p.Rms > 0.5)
            text += "\n警告: 半径残差偏大，建议增加角度数量";
        if (p.PointCount >= 5 && p.AxisRatio > 1.2)
            text += "\n警告: 轨迹不是正圆，检查标记提取或机械间隙";
        if (Math.Abs(p.ToolOffsetDeg) > 1e-9)
            text += $"\n工具零位偏角: {p.ToolOffsetDeg:0.00}°（输出第 4 轴角 = 零件角 − {p.ToolOffsetDeg:0.00}°）";
        return text;
    }
}
