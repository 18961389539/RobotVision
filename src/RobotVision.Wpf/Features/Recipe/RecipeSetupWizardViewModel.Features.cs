using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Teach;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;
internal sealed partial class RecipeSetupWizardViewModel
{
    internal void ReleasePreview()
    {
        _previewBuffer = null;
        _previewBitmap = null;
        Preview = null;
        FeatureOverlayRoi = null;
    }

    private void SyncFeatureOverlay() =>
        FeatureOverlayRoi = SelectedFeatureRoi();

    private Roi? SelectedFeatureRoi() =>
        SelectedFeatureIndex <= 0
            ? null
            : (uint)(SelectedFeatureIndex - 1) < (uint)_featureRanks.Count
                ? _featureRanks[SelectedFeatureIndex - 1].Roi
                : _featureRoi;

    private void RebuildFeatureRoiRows()
    {
        var rows = new List<FeatureRoiRow>(1 + _featureRanks.Count)
        {
            new("整颗目标", "不裁局部", SelectedFeatureIndex <= 0),
        };
        for (var i = 0; i < _featureRanks.Count; i++)
        {
            var c = _featureRanks[i];
            rows.Add(new($"{c.SizePx}×{c.SizePx}", $"{c.Gap:0.00}", SelectedFeatureIndex == i + 1));
        }

        FeatureRoiRows = rows;
        OnPropertyChanged(nameof(HasFeatureRoiRows));
        OnPropertyChanged(nameof(ShowFeatureRoiPicker));
        UpdateFeatureRoiHint();
    }

    private void UpdateFeatureRoiHint()
    {
        if (!ShowFeatureRoiPicker)
        {
            FeatureRoiHint = _chosen?.Refine == SegmentRefineMethod.Sift
                ? "SIFT 必须示教整颗目标，不能只裁局部特征框。"
                : "当前方法不需要模板训练区域。";
            return;
        }

        FeatureRoiHint = _featureRanks.Count == 0
            ? "点选「整颗目标」用分割转正全图示教。没有足够不对称的局部块时，也可采用后在配方页点「框选特征」手动画橙色框（丝印/齿脚）。"
            : "NCC / 形状匹配的示教裁剪：点表中一行。「整颗目标」= 分割转正全图；其余为相对壳体短边的局部窗口（预览金框）。也可采用后在配方页「框选特征」手动画。SIFT 请勿用局部框。";
    }
}
