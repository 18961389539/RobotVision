using FluentAssertions;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Cameras;
using RobotVision.Infrastructure.Communication;
using RobotVision.WpfHost;

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
    private readonly CameraManager _cameras = new();
    private readonly VisionService _vision;
    private readonly TcpServerManager _tcp;
    private readonly RobotVision.Infrastructure.Calibration.CalibrationManager _calibration = new();
    private readonly RobotVision.Infrastructure.Inference.ModelManager _models;
    private readonly RobotVision.Infrastructure.Lighting.LightingManager _lighting = new();
    private readonly RobotVision.Infrastructure.Inference.Strategies.AngleStrategyTypeRegistry _angleRegistry = new();

    public RecipeViewModelTests()
    {
        _recipeFolder = _dir.CreateSub("recipes");
        File.WriteAllText(System.IO.Path.Combine(_recipeFolder, "A01.json"),
            """{"cameraId": "cam_file", "stationId": "", "debugPassthrough": true, "angleMode": "KeyPointLine", "models": ["a.onnx"], "keypointIndexA": 0, "keypointIndexB": 1}""");
        File.WriteAllText(System.IO.Path.Combine(_recipeFolder, "B02.json"),
            """{"cameraId": "cam_file", "stationId": "st1", "angleMode": "MaskMinAreaRect", "models": ["b.onnx"], "confidence": 0.4}""");
        File.WriteAllText(System.IO.Path.Combine(_recipeFolder, "BAD.json"),
            """{"cameraId": "no_such_camera", "angleMode": "UnknownMode"}""");

        var replay = _dir.CreateSub("replay");
        using (var img = new OpenCvSharp.Mat(32, 32, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(90)))
            OpenCvSharp.Cv2.ImWrite(System.IO.Path.Combine(replay, "f.bmp"), img);
        _cameras.Register(new FileCamera("cam_file", replay));

        _loader = new RecipeLoader(_recipeFolder);
        _models = new RobotVision.Infrastructure.Inference.ModelManager(_dir.Path);
        _vision = TestInfra.CreateVisionService(_recipeFolder, _cameras);
        _tcp = TestInfra.CreateTcp();
    }

    public void Dispose()
    {
        _tcp.Dispose();
        _dir.Dispose();
    }

    private RecipeViewModel CreateVm() =>
        new(_loader, _cameras, _models, _calibration, _vision, _lighting, _angleRegistry, _tcp);

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
            vm.Editor.Name.Should().Be("A01_copy"); // 复制命名规则：原名 + _copy
            vm.HasUnsavedChanges.Should().BeFalse(); // 复制即基线，未修改
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
}
