using RobotVision.Core.Recipe;
using RobotVision.Teach;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>示教/向导共用：是否把分析建议的检测门限写入编辑器。</summary>
internal static class RecipeDetectionGatePrompt
{
    public static bool WouldChange(SegmentRefineAdvice advice, RecipeConfig editor) =>
        (advice.SuggestedConfidence > 0 &&
         Math.Abs(editor.Confidence - advice.SuggestedConfidence) > 0.02) ||
        (advice.SuggestedPixelConfidence > 0 &&
         Math.Abs(editor.Segmentation.PixelConfidence - advice.SuggestedPixelConfidence) > 0.02);

    public static bool TryConfirmAndApply(SegmentRefineAdvice advice, RecipeConfig editor, IDialogService dialogs)
    {
        if (!WouldChange(advice, editor))
            return false;
        if (!Confirm(advice, dialogs))
            return false;
        Apply(advice, editor);
        return true;
    }

    public static void Apply(SegmentRefineAdvice advice, RecipeConfig editor)
    {
        if (advice.SuggestedConfidence > 0)
            editor.Confidence = advice.SuggestedConfidence;
        if (advice.SuggestedPixelConfidence > 0)
            editor.Segmentation.PixelConfidence = advice.SuggestedPixelConfidence;
    }

    private static bool Confirm(SegmentRefineAdvice advice, IDialogService dialogs) =>
        dialogs.ConfirmYesNo(
            $"建议把检测置信度改为 {advice.SuggestedConfidence:0.00}、分割像素阈值改为 {advice.SuggestedPixelConfidence:0.00}。是否写入编辑器？",
            "采用示教分析阈值",
            questionIcon: true);
}
