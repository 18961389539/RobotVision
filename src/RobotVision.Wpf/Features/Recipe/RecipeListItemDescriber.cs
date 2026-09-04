using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Teach;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方列表项摘要（轻量元数据 + 校验，不读 templateImageBase64）。</summary>
internal static class RecipeListItemDescriber
{
    internal static RecipeListItem Describe(RecipeLoader loader, string name)
    {
        var meta = loader.PeekListMetadata(name);
        if (!meta.ParseSucceeded)
            return new RecipeListItem(name, meta.ParseError ?? "解析失败", false);

        try
        {
            var r = meta.ToValidationStub();
            RecipeLoader.Validate(r);
            loader.ValidateReferences(r);

            var mode = r.AngleMode switch
            {
                AngleMode.MaskMinAreaRect => "分割",
                AngleMode.DualCenterLine => "双模型",
                AngleMode.KeyPointLine => "关键点",
                AngleMode.MaskTemplate => r.Template.RefineMethod switch
                {
                    SegmentRefineMethod.Template => "分割+模板",
                    SegmentRefineMethod.Sift => "分割+SIFT",
                    SegmentRefineMethod.ShapeMatch => "分割+形状",
                    SegmentRefineMethod.LineFit => "分割+直线",
                    SegmentRefineMethod.CentroidHoleLine => "分割+孔槽",
                    SegmentRefineMethod.CaliperTab => "分割+卡尺",
                    _ => "分割+精修",
                },
                AngleMode.DualBlobCenterLine => "双BLOB",
                _ => r.AngleMode.ToString(),
            };
            var tags = new List<string> { mode, r.CameraId, r.Models.FirstOrDefault("") };
            tags.Add(r.SerialNumber > 0 ? $"#{r.SerialNumber}" : "无序号");
            if (!string.IsNullOrWhiteSpace(r.StationId))
                tags.Add($"工位:{r.StationId}");
            if (r.Roi is not null)
                tags.Add("ROI");
            if (meta.HasFeatureRoi &&
                TemplateOptions.UsesFeatureTeachRoi(r.Template.RefineMethod))
                tags.Add("特征框");
            if (meta.HasLighting)
                tags.Add($"光:{meta.LightControllerId}");
            if (meta.HasOutputOffset)
                tags.Add("补偿");
            if (meta.HasModelPin || meta.HasStationPin)
                tags.Add("钉扎");
            return new RecipeListItem(name, string.Join(" · ", tags), true, meta.Enabled, meta.Description);
        }
        catch (Exception ex)
        {
            return new RecipeListItem(name, ex.Message, false);
        }
    }
}
