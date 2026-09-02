namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>
/// 配方编辑器派生 UI 绑定名清单（单一来源）。
/// <see cref="RecipeViewModel.NotifyEditorMutated"/> 与
/// <see cref="RecipeViewModel.NotifyEditorBindings"/> 共用，避免双份手工列表漂移。
/// </summary>
internal static class RecipeEditorUiRefresh
{
    internal static readonly string[] PropertyNames =
    [
        nameof(RecipeViewModel.PrimaryModel),
        nameof(RecipeViewModel.SecondaryModel),
        nameof(RecipeViewModel.RotationCenterHint),
        nameof(RecipeViewModel.MappingHint),
        nameof(RecipeViewModel.AngleModeHint),
        nameof(RecipeViewModel.UndirectedEccentricHint),
        nameof(RecipeViewModel.IsDualMode),
        nameof(RecipeViewModel.IsKeyPointMode),
        nameof(RecipeViewModel.IsSegmentationMode),
        nameof(RecipeViewModel.IsMaskTemplateMode),
        nameof(RecipeViewModel.IsDualBlobMode),
        nameof(RecipeViewModel.IsTemplateMethod),
        nameof(RecipeViewModel.UsesFeatureTeachRoi),
        nameof(RecipeViewModel.UsesRefineLine),
        nameof(RecipeViewModel.NeedsTaughtTemplate),
        nameof(RecipeViewModel.ShowRefineRange),
        nameof(RecipeViewModel.HasTemplate),
        nameof(RecipeViewModel.ShowBlobFixedThreshold),
        nameof(RecipeViewModel.ShowDualCropExpand),
        nameof(RecipeViewModel.RefineMethodHint),
        nameof(RecipeViewModel.RefineDetailsSummary),
        nameof(RecipeViewModel.TeachPeakHint),
        nameof(RecipeViewModel.TeachDiagnosticsHint),
        nameof(RecipeViewModel.PolarityLockHint),
        nameof(RecipeViewModel.FeatureGrabOriginHint),
        nameof(RecipeViewModel.TeachGeometryHint),
        nameof(RecipeViewModel.OutputOffsetTeachHint),
        nameof(RecipeViewModel.TemplateStatusText),
        nameof(RecipeViewModel.HasAnyImage),
        nameof(RecipeViewModel.ShowTestImageViewer),
        nameof(RecipeViewModel.ShowRoiImageViewer),
        nameof(RecipeViewModel.VisibleRecipes),
        nameof(RecipeViewModel.RecipeHealthHint),
    ];
}
