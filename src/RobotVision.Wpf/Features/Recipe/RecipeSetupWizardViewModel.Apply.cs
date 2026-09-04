using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Teach;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;
internal sealed partial class RecipeSetupWizardViewModel
{
    private bool CanApply => !IsBusy && _chosen is not null;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        ApplyRecommendationToEditor();
        Applied = true;
        _host.Message = $"配置工作台已写入编辑器：{_chosen!.Title}（未保存）";
        RequestClose?.Invoke();
    }

    private void FlushPendingNumbers()
    {
        this.Commit();
        _host.CommitEdits();
    }

    private void ApplyRecommendationToEditor()
    {
        FlushPendingNumbers();
        if (_chosen is null)
            return;
        var chosen = _chosen;
        var editor = _host.Editor;
        var previous = editor.AngleMode;
        editor.AngleMode = chosen.AngleMode;
        if (chosen.AngleMode == AngleMode.MaskTemplate && chosen.Refine is { } refine)
        {
            editor.Template.RefineMethod = refine;
            editor.Template.UseEdgeMatch = chosen.EdgeMatch;
            if (_edgePolarity != HousingEdgePolarity.Auto)
                editor.Template.HousingEdgePolarity = _edgePolarity;
            if (_tabPolarity != TabPolarityLock.Auto)
                editor.Template.TabPolarity = _tabPolarity;
            WriteLocks(refine);
            ApplyParamTune();
            if (TemplateOptions.UsesFeatureTeachRoi(refine))
            {
                if (SelectedFeatureRoi() is { } roi)
                    _host.ApplySuggestedFeatureRoi(roi);
                else
                    _host.Editor.Template.Roi = null;
            }
            else if (refine == SegmentRefineMethod.Sift)
                _host.Editor.Template.Roi = null;
        }

        editor.Template.ExpectedCount = Math.Clamp(Constraints.ExpectedCount, 0, 20);
        RecipeEditorModeCleanup.Apply(editor, previous);

        _host.RefreshEditorBindings();
        OnPropertyChanged(nameof(ShowTeachActions));
        OnPropertyChanged(nameof(ShowFeatureDraw));
        _test.NotifyCanExecuteChanged();
    }

    private void WriteLocks(SegmentRefineMethod refine)
    {
        if (_refineAdvice is not { } locks)
            return;
        if (refine == SegmentRefineMethod.Template && locks.TeachPeakScore >= 0.3)
        {
            _host.Editor.Template.TeachPeakScore = locks.TeachPeakScore;
            if (locks.SuggestedMatchThreshold > 0)
                _host.Editor.Template.MatchThreshold = locks.SuggestedMatchThreshold;
        }
        if (locks.TeachAreaPx > 1)
            _host.Editor.Template.TeachAreaPx = locks.TeachAreaPx;
        if (locks.Aspect > 1e-3)
            _host.Editor.Template.TeachAspect = locks.Aspect;
        RecipeDetectionGatePrompt.TryConfirmAndApply(locks, _host.Editor, _test.Dialogs);
    }

    private void ApplyParamTune()
    {
        if (_paramTune is not { } tune)
            return;
        var t = _host.Editor.Template;
        if (tune.MatchThreshold is { } th)
            t.MatchThreshold = th;
        if (tune.RefineRangeDeg is { } range)
            t.SetSymmetricRefineRange(range);
        if (tune.UseEdgeMatch is { } edge)
            t.UseEdgeMatch = edge;
        if (tune.ExpectedCount is { } n)
            t.ExpectedCount = n;
        if (tune.EdgePolarity is { } ep)
            t.HousingEdgePolarity = ep;
        if (tune.TabPolarity is { } tp)
            t.TabPolarity = tp;
    }

    [RelayCommand]
    private void ChoosePrimary()
    {
        if (_playbook is null)
            return;
        _userPicked = true;
        SelectCandidate(_playbook.Primary);
    }

    [RelayCommand]
    private void ChooseAlternative(PlaybookCandidate? candidate)
    {
        if (candidate is null)
            return;
        _userPicked = true;
        SelectCandidate(candidate);
    }
}
