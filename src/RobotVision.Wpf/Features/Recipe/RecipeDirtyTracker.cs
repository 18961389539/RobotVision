using RobotVision.Core.Recipe;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方编辑器脏标记：基线指纹与当前编辑器对比。</summary>
internal sealed class RecipeDirtyTracker
{
    private string _baselineBodyFingerprint = "";
    private string _baselineTemplateImage = "";
    private bool _hasUnsavedChanges;

    public bool HasUnsavedChanges => _hasUnsavedChanges;

    public void ResetFromBaseline(RecipeConfig? baseline, RecipeConfig? editor = null)
    {
        if (baseline is null)
        {
            _baselineBodyFingerprint = "";
            _baselineTemplateImage = "";
            _hasUnsavedChanges = false;
            return;
        }

        _baselineTemplateImage = baseline.Template.TemplateImageBase64 ?? "";
        _baselineBodyFingerprint = RecipeCompare.BodyFingerprint(baseline);
        _hasUnsavedChanges = editor is null ? false : Evaluate(editor, baseline);
    }

    public bool Evaluate(RecipeConfig editor, RecipeConfig? baseline)
    {
        if (baseline is null)
            return false;
        if (!string.Equals(editor.Template.TemplateImageBase64, _baselineTemplateImage, StringComparison.Ordinal))
            return true;
        return RecipeCompare.BodyFingerprint(editor) != _baselineBodyFingerprint;
    }

    /// <returns>true 当脏状态发生变化。</returns>
    public bool TryPublish(RecipeConfig editor, RecipeConfig? baseline, Action onChanged)
    {
        var dirty = Evaluate(editor, baseline);
        if (_hasUnsavedChanges == dirty)
            return false;
        _hasUnsavedChanges = dirty;
        onChanged();
        return true;
    }
}
