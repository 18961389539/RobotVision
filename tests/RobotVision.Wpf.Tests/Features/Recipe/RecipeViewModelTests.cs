using System.ComponentModel;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.WpfHost.Features.Recipe;
using RobotVision.WpfHost.Shared;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 配方管理页测试：列表加载、搜索过滤（VisibleRecipes）、脏标记
/// （HasUnsavedChanges/UnsavedHint）、新建/复制切换、无效配方标记。
/// 触发/示教等依赖相机取图与真实推理的路径不在本类覆盖（见集成测试）。
/// </summary>
public class RecipeViewModelTests : IDisposable
{
    private readonly TestInfra.TempDir _dir = new("rv_recipe");
    private readonly string _recipeFolder;
    private readonly RecipeLoader _loader;
    private readonly AppConfig _cfg;
    private readonly CameraManager _cameras = new();
    private readonly VisionService _vision;
    private readonly TcpServerManager _tcp;
    private readonly RobotVision.Infrastructure.Calibration.CalibrationManager _calibration = new();
    private readonly RobotVision.Infrastructure.Inference.ModelManager _models;
    private readonly RobotVision.Infrastructure.Lighting.LightingManager _lighting = new();
    private readonly RobotVision.Infrastructure.Inference.Strategies.AngleStrategyTypeRegistry _angleRegistry = new();
    private readonly AssetIntegrityChecker _assets;
    private readonly TestDialogService _dialogs = new();
    private readonly NullRecipeWindowService _recipeWindows = new();

    public RecipeViewModelTests()
    {
        _recipeFolder = _dir.CreateSub("recipes");
        File.WriteAllText(System.IO.Path.Combine(_recipeFolder, "A01.json"),
            """{"cameraId": "cam_file", "stationId": "", "angleMode": "KeyPointLine", "models": ["a.onnx"], "keypointIndexA": 0, "keypointIndexB": 1}""");
        File.WriteAllText(System.IO.Path.Combine(_recipeFolder, "B02.json"),
            """{"cameraId": "cam_file", "stationId": "st1", "angleMode": "MaskMinAreaRect", "models": ["b.onnx"], "confidence": 0.4}""");
        File.WriteAllText(System.IO.Path.Combine(_recipeFolder, "BAD.json"),
            """{"cameraId": "no_such_camera", "angleMode": "UnknownMode"}""");

        var replay = _dir.CreateSub("replay");
        using (var img = new OpenCvSharp.Mat(32, 32, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(90)))
            OpenCvSharp.Cv2.ImWrite(System.IO.Path.Combine(replay, "f.bmp"), img);
        _cameras.Register(new FileCamera("cam_file", replay));

        _cfg = TestInfra.CreateAppConfig(_dir.Path);
        _cfg.Cameras.Add(new CameraConfig { Id = "cam_file", Type = "File", Folder = replay });

        _loader = new RecipeLoader(_recipeFolder);
        _models = new RobotVision.Infrastructure.Inference.ModelManager(_dir.Path);
        _assets = new AssetIntegrityChecker(
            new AppConfig(), _models, _calibration, NullLogger<AssetIntegrityChecker>.Instance);
        _vision = TestInfra.CreateVisionService(_recipeFolder, _cameras);
        _tcp = TestInfra.CreateTcp();
    }

    public void Dispose()
    {
        _models.Dispose();
        _lighting.Dispose();
        _calibration.Dispose();
        _cameras.Dispose();
        _tcp.Dispose();
        _dir.Dispose();
    }

    private RecipeViewModel CreateVm() =>
        new(_loader, _cfg, TestInfra.CameraFacade(_cameras), TestInfra.ModelFacade(_models),
            TestInfra.CalibrationFacade(_calibration), _vision, TestInfra.LightingFacade(_lighting),
            TestInfra.AngleCatalog(_angleRegistry), _assets, _dialogs, _recipeWindows, TestLog.Null<RecipeViewModel>());

    [Fact]
    public void Ctor_LoadsRecipeList_SelectsFirst()
    {
        var vm = CreateVm();
        try
        {
            vm.Recipes.Should().HaveCount(3);
            vm.Selected.Should().NotBeNull();
            vm.Editor.Should().NotBeNull();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void SearchText_FiltersVisibleRecipes_ByNameOrDescription()
    {
        var vm = CreateVm();
        try
        {
            vm.VisibleRecipes.Should().HaveCount(3);

            vm.SearchText = "A01";
            vm.VisibleRecipes.Should().ContainSingle(r => r.Name == "A01");

            vm.SearchText = "zzz";
            vm.VisibleRecipes.Should().BeEmpty();

            vm.SearchText = "";
            vm.VisibleRecipes.Should().HaveCount(3);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void InvalidRecipe_IsMarkedInvalid_AndSelectable()
    {
        var vm = CreateVm();
        try
        {
            var bad = vm.Recipes.Single(r => r.Name == "BAD");
            bad.IsValid.Should().BeFalse(); // 非法配置 → 校验失败
            bad.Summary.Should().NotBeEmpty(); // 展示失败原因（JSON 解析/引用校验）
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void NewRecipe_FlipsIsNew_AndPreparesEditor()
    {
        var vm = CreateVm();
        try
        {
            vm.IsNew.Should().BeFalse();

            vm.NewCommand.Execute(null);

            vm.IsNew.Should().BeTrue();
            vm.Selected.Should().BeNull("新建草稿不应再高亮列表中的已存配方");
            vm.Editor.Name.Should().Be("");
            vm.Editor.Models.Should().ContainSingle(m => m == ""); // 新配方预留模型槽
            // 新建即对齐基线：尚未修改时无未保存标记，改动后才出现
            vm.HasUnsavedChanges.Should().BeFalse();
            vm.UnsavedHint.Should().BeEmpty();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void CopyRecipe_KeepsEditorContent_AndMarksNew()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "A01");
            // Refresh 的 _switching 保护跳过了构造期 Editor 加载：先切空再切目标触发加载
            vm.Selected = null;
            vm.Selected = target;
            vm.Editor.Name.Should().Be("A01"); // 已加载目标配方

            vm.CopyCommand.Execute(null);

            vm.IsNew.Should().BeTrue();
            vm.Selected.Should().BeNull("复制草稿不应再高亮源配方");
            vm.Editor.Name.Should().Be("A01_copy"); // 复制命名规则：原名 + _copy
            vm.Editor.SerialNumber.Should().Be(0);
            vm.DeleteCommand.CanExecute(null).Should().BeFalse();
            vm.HasUnsavedChanges.Should().BeFalse(); // 复制即基线，未修改
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void Roi_Enable_MarksUnsaved()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "A01");
            vm.Selected = null;
            vm.Selected = target;
            vm.HasUnsavedChanges.Should().BeFalse();

            vm.Roi.UseRoi = true;

            vm.HasUnsavedChanges.Should().BeTrue();
            vm.Editor.Roi.Should().NotBeNull();
            vm.UnsavedHint.Should().Contain("试触发已用当前编辑器");
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void ToggleCommands_FlipPanels()
    {
        var vm = CreateVm();
        try
        {
            var list = vm.IsListPanelVisible;
            vm.ToggleListPanelCommand.Execute(null);
            vm.IsListPanelVisible.Should().Be(!list);

            var param = vm.IsParamPanelVisible;
            vm.ToggleParamPanelCommand.Execute(null);
            vm.IsParamPanelVisible.Should().Be(!param);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void EditorAngleMode_ExposesModeFlags()
    {
        var vm = CreateVm();
        try
        {
            vm.Editor.AngleMode = RobotVision.Core.Recipe.AngleMode.KeyPointLine;
            vm.NotifyEditorMutated();
            vm.IsKeyPointMode.Should().BeTrue();
            vm.IsDualMode.Should().BeFalse();
            vm.IsSegmentationMode.Should().BeFalse();
            vm.IsMaskTemplateMode.Should().BeFalse();

            vm.Editor.AngleMode = RobotVision.Core.Recipe.AngleMode.DualCenterLine;
            vm.NotifyEditorMutated();
            vm.IsDualMode.Should().BeTrue();

            vm.Editor.AngleMode = RobotVision.Core.Recipe.AngleMode.MaskMinAreaRect;
            vm.NotifyEditorMutated();
            vm.IsSegmentationMode.Should().BeTrue();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void RefreshEditorBindings_RaisesEditorSoComboBoxCanSwitch()
    {
        var vm = CreateVm();
        try
        {
            var editorPings = 0;
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(RecipeViewModel.Editor))
                    editorPings++;
            };

            vm.NotifyEditorMutated();
            editorPings.Should().Be(0, "脏轮询不能 Ping Editor，否则会打断正在改的输入框");

            ((IRecipeWorkspace)vm).RefreshEditorBindings();
            editorPings.Should().Be(1);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void Save_PersistsSerialNumber_AndReloadsIt()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "A01");
            vm.Selected = null;
            vm.Selected = target;
            vm.Editor.SerialNumber = 1;

            vm.SaveCommand.Execute(null);

            vm.Message.Should().NotStartWith("保存失败");
            vm.Editor.SerialNumber.Should().Be(1);
            _loader.Get("A01", forceReload: true).SerialNumber.Should().Be(1);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void Save_Rename_RemovesOldRecipe_AndKeepsNew()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "A01");
            vm.Selected = null;
            vm.Selected = target;
            vm.Editor.Name.Should().Be("A01");

            vm.Editor.Name = "A01b";
            vm.SaveCommand.Execute(null);

            vm.Message.Should().NotStartWith("保存失败");
            File.Exists(Path.Combine(_recipeFolder, "A01.json")).Should().BeFalse();
            File.Exists(Path.Combine(_recipeFolder, "A01b.json")).Should().BeTrue();
            vm.Recipes.Select(r => r.Name).Should().NotContain("A01");
            vm.Recipes.Select(r => r.Name).Should().Contain("A01b");
            vm.Selected!.Name.Should().Be("A01b");
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void Copy_ThenSave_KeepsSourceRecipe()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "A01");
            vm.Selected = null;
            vm.Selected = target;

            vm.CopyCommand.Execute(null);
            vm.Editor.Name.Should().Be("A01_copy");
            vm.SaveCommand.Execute(null);

            vm.Message.Should().NotStartWith("保存失败");
            File.Exists(Path.Combine(_recipeFolder, "A01.json")).Should().BeTrue();
            File.Exists(Path.Combine(_recipeFolder, "A01_copy.json")).Should().BeTrue();
            vm.Recipes.Select(r => r.Name).Should().Contain("A01");
            vm.Recipes.Select(r => r.Name).Should().Contain("A01_copy");
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void Copy_ClearsSerialNumberAndOutputOffset()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "A01");
            vm.Selected = null;
            vm.Selected = target;
            vm.Editor.SerialNumber = 3;
            vm.Editor.OutputOffset.X = 0.25;
            vm.Editor.OutputOffset.TeachX = 10;
            vm.Editor.OutputOffset.TeachY = 20;
            vm.Editor.OutputOffset.TeachRzDeg = 1;
            vm.SaveCommand.Execute(null);
            vm.Message.Should().NotStartWith("保存失败");
            vm.HasUnsavedChanges.Should().BeFalse();

            vm.CopyCommand.Execute(null);

            vm.IsNew.Should().BeTrue();
            vm.Editor.Name.Should().Be("A01_copy");
            vm.Editor.SerialNumber.Should().Be(0);
            vm.Editor.OutputOffset.IsZero.Should().BeTrue();
            vm.Editor.OutputOffset.HasTeachOutput.Should().BeFalse();
            _loader.Get("A01", forceReload: true).SerialNumber.Should().Be(3);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void Copy_ClearsSelection_SoDeleteCannotRemoveSource()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "A01");
            vm.Selected = null;
            vm.Selected = target;

            vm.CopyCommand.Execute(null);

            vm.Selected.Should().BeNull();
            vm.DeleteCommand.CanExecute(null).Should().BeFalse();
            File.Exists(Path.Combine(_recipeFolder, "A01.json")).Should().BeTrue();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void Refresh_IgnoreUnsaved_RebuildsListAfterFileDeletedWhileEditorDirty()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "B02");
            vm.Selected = null;
            vm.Selected = target;
            vm.Editor.Confidence = 0.88f;
            vm.NotifyEditorMutated();
            vm.HasUnsavedChanges.Should().BeTrue();

            _loader.Delete("B02");
            vm.Refresh(preferName: string.Empty, reloadEditor: true, ignoreUnsaved: true);

            vm.Recipes.Should().NotContain(r => r.Name == "B02");
            vm.Selected.Should().NotBeNull();
            vm.Selected!.Name.Should().NotBe("B02");
            vm.HasUnsavedChanges.Should().BeFalse();
            File.Exists(Path.Combine(_recipeFolder, "B02.json")).Should().BeFalse();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void MaskTemplateWithoutTeach_ExposesBlockReason()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "B02");
            vm.Selected = null;
            vm.Selected = target;
            vm.Editor.AngleMode = AngleMode.MaskTemplate;
            vm.Editor.Template.RefineMethod = SegmentRefineMethod.Template;
            vm.Editor.Template.TemplateImageBase64 = "";
            vm.NotifyEditorMutated();

            vm.CanTestTrigger.Should().BeFalse();
            vm.TestTriggerBlockHint.Should().NotBeNullOrEmpty();
            vm.TestTriggerBlockHint.Should().StartWith("无法测试触发：");
            vm.ShowTestTriggerBlockHint.Should().BeTrue();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void IsBusy_DisablesSaveCopyDeleteAndShowsIdle()
    {
        var vm = CreateVm();
        try
        {
            vm.IsIdle.Should().BeTrue();
            vm.SaveCommand.CanExecute(null).Should().BeTrue();
            vm.CopyCommand.CanExecute(null).Should().BeTrue();
            vm.Selected = vm.Recipes.First();
            vm.DeleteCommand.CanExecute(null).Should().BeTrue();

            vm.IsBusy = true;
            vm.IsIdle.Should().BeFalse();
            vm.SaveCommand.CanExecute(null).Should().BeFalse();
            vm.CopyCommand.CanExecute(null).Should().BeFalse();
            vm.DeleteCommand.CanExecute(null).Should().BeFalse();
            vm.RefreshCommand.CanExecute(null).Should().BeFalse();
            vm.Roi.PreviewRoiCommand.CanExecute(null).Should().BeFalse();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void ClearAssetPins_RefreshesAssetPinStatus()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "A01");
            vm.Selected = null;
            vm.Selected = target;
            vm.Editor.ModelSha256 = ["abc"];
            vm.NotifyEditorMutated();
            vm.AssetPinStatus.Should().NotBe(AssetPinStatusText.Unpinned);

            vm.ClearAssetPinsCommand.Execute(null);
            vm.AssetPinStatus.Should().Be(AssetPinStatusText.Unpinned);
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void SearchFilter_HidesSelectedFromList_DisablesDelete()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "B02");
            vm.Selected = null;
            vm.Selected = target;
            vm.IsSelectedVisibleInFilter.Should().BeTrue();
            vm.DeleteCommand.CanExecute(null).Should().BeTrue();

            vm.SearchText = "A01";
            vm.IsSelectedVisibleInFilter.Should().BeFalse();
            vm.SelectedFilterHint.Should().Contain("B02");
            vm.DeleteCommand.CanExecute(null).Should().BeFalse();
            vm.Selected!.Name.Should().Be("B02");

            vm.SearchText = "";
            vm.IsSelectedVisibleInFilter.Should().BeTrue();
            vm.DeleteCommand.CanExecute(null).Should().BeTrue();
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void LockedPolarity_ExposesPolarityLockHint()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "B02");
            vm.Selected = null;
            vm.Selected = target;
            vm.Editor.AngleMode = AngleMode.MaskTemplate;
            vm.Editor.Template.RefineMethod = SegmentRefineMethod.CaliperTab;
            vm.Editor.Template.HousingEdgePolarity = HousingEdgePolarity.BrightToDark;
            vm.Editor.Template.TabPolarity = TabPolarityLock.PlusShortAxis;
            vm.NotifyEditorMutated();

            vm.PolarityLockHint.Should().Contain("已锁定");
            vm.PolarityLockHint.Should().Contain("亮场");
            vm.PolarityLockHint.Should().Contain("凸起在+短轴");
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void FeatureRoi_OnTemplateMatch_ExposesGrabOriginHint()
    {
        var vm = CreateVm();
        try
        {
            var target = vm.Recipes.First(r => r.Name == "B02");
            vm.Selected = null;
            vm.Selected = target;
            vm.Editor.AngleMode = AngleMode.MaskTemplate;
            vm.Editor.Template.RefineMethod = SegmentRefineMethod.Template;
            vm.Editor.Template.Roi = new RobotVision.Core.Models.Roi(0.1, 0.4, 0.5, 0.08);
            vm.NotifyEditorMutated();

            vm.FeatureGrabOriginHint.Should().Contain("特征中心");
            vm.FeatureGrabOriginHint.Should().Contain("扁框");
            vm.RefineDetailsSummary.Should().Contain("十字=特征中心");
            vm.RefineDetailsSummary.Should().Contain("扁框易跳齿");
        }
        finally { vm.Dispose(); }
    }

    [Fact]
    public void SetupWizard_DetachForClose_DoesNotAccumulateSingletonHandlers()
    {
        var vm = CreateVm();
        try
        {
            for (var i = 0; i < 3; i++)
            {
                using var wizard = CreateWizard(vm);
                WizardSubscribesToHost(vm, wizard).Should().BeTrue();
                WizardSubscribesToTest(vm, wizard).Should().BeTrue();
            }

            CountWizardHostHandlers(vm).Should().Be(0);
            CountWizardTestHandlers(vm).Should().Be(0);

            using (var wizard = CreateWizard(vm))
            {
                WizardSubscribesToHost(vm, wizard).Should().BeTrue();
                wizard.Dispose();
                WizardSubscribesToHost(vm, wizard).Should().BeFalse();
                WizardSubscribesToTest(vm, wizard).Should().BeFalse();
            }
        }
        finally { vm.Dispose(); }
    }

    private static int CountWizardHostHandlers(RecipeViewModel host) =>
        CountHandlers(host, "OnHostPropertyChanged");

    private static int CountWizardTestHandlers(RecipeViewModel host) =>
        CountHandlers(host.Test, "OnTestPropertyChanged");

    private static int CountHandlers(INotifyPropertyChanged source, string methodName)
    {
        var handler = GetPropertyChangedHandlers(source);
        return handler is null
            ? 0
            : handler.GetInvocationList().Count(d => d.Method.Name == methodName);
    }

    private static bool WizardSubscribesToHost(RecipeViewModel host, RecipeSetupWizardViewModel wizard) =>
        HasHandler(host, wizard, "OnHostPropertyChanged");

    private static bool WizardSubscribesToTest(RecipeViewModel host, RecipeSetupWizardViewModel wizard) =>
        HasHandler(host.Test, wizard, "OnTestPropertyChanged");

    private static bool HasHandler(INotifyPropertyChanged source, object target, string methodName)
    {
        var handler = GetPropertyChangedHandlers(source);
        if (handler is null)
            return false;
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        return method is not null &&
               handler.GetInvocationList().Any(d => ReferenceEquals(d.Target, target) && d.Method == method);
    }

    private static PropertyChangedEventHandler? GetPropertyChangedHandlers(INotifyPropertyChanged source)
    {
        for (var type = source.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetField(
                         "PropertyChanged",
                         BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                     ?? type.GetField(
                         "<PropertyChanged>k__BackingField",
                         BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(source) is PropertyChangedEventHandler handler)
                return handler;
        }

        return null;
    }

    private RecipeSetupWizardViewModel CreateWizard(RecipeViewModel host) =>
        new(host, TestInfra.CameraFacade(_cameras), TestInfra.ModelFacade(_models),
            TestInfra.CalibrationFacade(_calibration), TestInfra.LightingFacade(_lighting), host.Roi, host.Test);
}
