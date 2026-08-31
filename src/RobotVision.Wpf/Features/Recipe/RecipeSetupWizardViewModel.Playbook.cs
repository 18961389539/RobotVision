using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using RobotVision.Core;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Inference;
using RobotVision.Infrastructure.Inference.Strategies;
using RobotVision.Teach;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.Recipe;
internal sealed partial class RecipeSetupWizardViewModel
{
    partial void OnSelectedBakeOffIndexChanged(int value)
    {
        if (_syncingSelection || (uint)value >= (uint)_bakeoff.Count)
            return;
        var row = _bakeoff[value];
        if (!ScenePlaybook.IsEligible(row.Method, Constraints, _scene))
        {
            Message = $"「{ScenePlaybook.RefineLabel(row.Method)}」无任务资格，未改采用项。";
            return;
        }

        _userPicked = true;
        var edge = row.Method == SegmentRefineMethod.Template &&
                   (_playbook?.Primary.EdgeMatch ?? false);
        SelectCandidate(new PlaybookCandidate(
            AngleMode.MaskTemplate, row.Method, edge,
            $"{ScenePlaybook.AngleModeLabel(AngleMode.MaskTemplate)} · {ScenePlaybook.RefineLabel(row.Method)}",
            $"已选赛马项：{row.Note}", true));
    }

    private void SelectCandidate(PlaybookCandidate candidate)
    {
        _chosen = candidate;
        ChosenHint = _playbook is { IsUncertain: true }
            ? $"把握不足，请先核备选。将采用：{candidate.Title}"
            : $"将采用：{candidate.Title}";
        AlternativeRows = (_playbook?.Alternatives ?? []).Select(a =>
            new WizardAltRow(a, a.Title, a.Why, ScenePlaybook.SameRecipe(a, candidate))).ToList();
        OnPropertyChanged(nameof(ChosenIsPrimary));
        OnPropertyChanged(nameof(ApplyLabel));
        ApplyCommand.NotifyCanExecuteChanged();
        SyncBakeOffSelection(candidate);
        RefreshParamTune();
        OnPropertyChanged(nameof(ShowFeatureRoiPicker));
        UpdateFeatureRoiHint();
    }

    private bool _syncingParamTune;

    private void RefreshParamTune()
    {
        _paramTune = null;
        ParamTuneRows = [];
        ParamTuneHint = "";
        if (_chosen?.Refine is not { } method || _perFrame.Count == 0)
        {
            OnPropertyChanged(nameof(HasParamTuneRows));
            return;
        }

        var peak = _refineAdvice is { TeachPeakScore: >= 0.3 } locks
            ? locks.TeachPeakScore
            : _host.Editor.Template.TeachPeakScore;
        _paramTune = RefineParamTuner.Tune(
            method,
            _perFrame,
            _instanceCounts,
            _host.Editor.Template,
            peak,
            _edgePolarity,
            _tabPolarity,
            Constraints.ExpectedCount,
            _scene?.Aspect ?? 0,
            _chosen.EdgeMatch);
        if (_paramTune is { } sug)
        {
            ParamTuneHint = sug.Summary;
            ParamTuneRows = sug.Trials.Select(t => new ParamTuneRow(t.Label, $"{t.Score:0.00}", t.Note, t.Best)).ToList();
        }
        else
            ParamTuneHint = "当前方法无可调门限，或还没有整夹分数（模板类请先示教再回放）。";

        _syncingParamTune = true;
        SelectedParamTuneIndex = ParamTuneRows.ToList().FindIndex(r => r.Best);
        _syncingParamTune = false;
        OnPropertyChanged(nameof(HasParamTuneRows));
    }

    partial void OnSelectedParamTuneIndexChanged(int value)
    {
        if (_syncingParamTune || _paramTune is not { } sug || (uint)value >= (uint)sug.Trials.Count)
            return;
        var trial = sug.Trials[value];
        if (trial.MatchThreshold <= 0)
            return;
        _paramTune = sug with
        {
            MatchThreshold = trial.MatchThreshold,
            Score = trial.Score,
            Trials = sug.Trials.Select((t, i) => t with { Best = i == value }).ToList(),
            Summary = $"已选匹配门 {trial.MatchThreshold:0.00}（{trial.Note}）。采用后请再试触发。",
        };
        ParamTuneHint = _paramTune.Summary;
        ParamTuneRows = _paramTune.Trials.Select(t => new ParamTuneRow(t.Label, $"{t.Score:0.00}", t.Note, t.Best)).ToList();
    }

    private void SyncBakeOffSelection(PlaybookCandidate candidate)
    {
        _syncingSelection = true;
        SelectedBakeOffIndex = candidate.Refine is { } m
            ? BakeOffRows.ToList().FindIndex(r => r.MethodId == m)
            : -1;
        _syncingSelection = false;
    }

    private void RefreshPlaybook()
    {
        var advice = ScenePlaybook.Recommend(Constraints, _scene, _bakeoff, CurrentPrior, _sceneVotes);
        _playbook = advice;
        PlaybookSummary = advice.Summary;
        ConfidenceNote = advice.ConfidenceNote;
        PrimaryTitle = advice.Primary.Title;
        PrimaryWhy = advice.Primary.Why;
        BakeOffRows = _bakeoff.Select(c => new BakeOffRow(
            c.Method,
            ScenePlaybook.RefineLabel(c.Method),
            $"{c.Score:0.00}",
            c.Note,
            c.Ok,
            ScenePlaybook.IsEligible(c.Method, Constraints, _scene))).ToList();

        var keep = _userPicked && _chosen is { } prev &&
                   (ScenePlaybook.SameRecipe(prev, advice.Primary) ||
                    advice.Alternatives.Any(a => ScenePlaybook.SameRecipe(a, prev)) ||
                    (prev.Refine is { } m && _bakeoff.Any(c => c.Method == m && ScenePlaybook.IsEligible(m, Constraints, _scene))));
        SelectCandidate(keep && _chosen is { } chosen ? chosen : advice.Primary);
        if (!keep)
            _userPicked = false;

        ApplyCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(NextLabel));
        OnPropertyChanged(nameof(HasEnoughForResult));
        OnPropertyChanged(nameof(IsFileCamera));
        OnPropertyChanged(nameof(CameraHint));
        OnPropertyChanged(nameof(AnalyzeHint));
        OnPropertyChanged(nameof(ApplyLabel));
    }
}
