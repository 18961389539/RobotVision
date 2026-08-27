using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using RobotVision.Core.Recipe;
using RobotVision.Hosting;
using RobotVision.Hosting.Cameras;
using RobotVision.Infrastructure.Cameras;
using RobotVision.WpfHost.Features.Cameras;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 相机管理页：保存后应保留当前预览图（图仍属于这台相机）；
/// 切换到另一台相机时才清空，避免误把旧机画面当成新机画面。
/// </summary>
public class CamerasViewModelTests : IDisposable
{
    private readonly TestInfra.TempDir _dir = new("rv_cameras");
    private readonly AppConfig _cfg;
    private readonly CameraConfigStore _store;
    private readonly CameraManager _cameras = new();
    private readonly RecipeLoader _recipes;
    private readonly CameraTypeRegistry _registry = CameraTypeRegistry.CreateDefault();

    public CamerasViewModelTests()
    {
        _cfg = TestInfra.CreateAppConfig(_dir.Path);
        _cfg.Cameras =
        [
            new CameraConfig { Id = "cam_a", Type = "Virtual", Width = 64, Height = 64, Pattern = "Bars" },
            new CameraConfig { Id = "cam_b", Type = "Virtual", Width = 64, Height = 64, Pattern = "Bars" },
        ];
        _cameras.Register(new VirtualCamera("cam_a", 64, 64, "Bars"));
        _cameras.Register(new VirtualCamera("cam_b", 64, 64, "Bars"));
        Directory.CreateDirectory(_cfg.RecipesFolder);
        _recipes = new RecipeLoader(_cfg.RecipesFolder);
        _store = new CameraConfigStore(_cfg, Path.Combine(_dir.Path, "appsettings.json"));
    }

    public void Dispose()
    {
        _cameras.Dispose();
        _dir.Dispose();
    }

    private CamerasViewModel CreateVm() =>
        new(_cfg, _store, _cameras, _recipes, _registry);

    private static BitmapSource TinyImage() =>
        BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 0, 255 }, 4);

    [Fact]
    public void Save_KeepsPreviewImage_ForSameCamera()
    {
        TestInfra.RunSta(() =>
        {
            var vm = CreateVm();
            vm.Selected!.Id.Should().Be("cam_a");

            var image = TinyImage();
            vm.PreviewImage = image;
            vm.PreviewCaption = "测试取图 · cam_a（Virtual）";

            vm.SaveCommand.Execute(null);

            vm.Message.Should().StartWith("已保存 cam_a");
            vm.PreviewImage.Should().BeSameAs(image);
            vm.PreviewCaption.Should().Be("测试取图 · cam_a（Virtual）");
        });
    }

    [Fact]
    public void Save_PersistsOptionalDisplayName()
    {
        TestInfra.RunSta(() =>
        {
            var vm = CreateVm();
            vm.Selected = vm.Items.Single(i => i.Id == "cam_a");
            vm.EditName = "左工位";

            vm.SaveCommand.Execute(null);

            vm.Message.Should().StartWith("已保存 cam_a");
            _cfg.Cameras.Single(c => c.Id == "cam_a").Name.Should().Be("左工位");
            vm.Items.Single(i => i.Id == "cam_a").Title.Should().Be("左工位");
            vm.Items.Single(i => i.Id == "cam_a").Subtitle.Should().Be("cam_a");
        });
    }

    [Fact]
    public void TestGrab_ShowsResolutionElapsedAndCaptureTime()
    {
        TestInfra.RunSta(() =>
        {
            var vm = CreateVm();
            vm.TestGrabCommand.ExecuteAsync(null).GetAwaiter().GetResult();

            vm.PreviewCaption.Should().Contain("测试取图");
            vm.PreviewCaption.Should().Contain("64×64");
            vm.PreviewCaption.Should().Contain("ms");
            vm.PreviewToolTip.Should().StartWith("采集时刻");
            vm.Message.Should().Contain("ms");
        });
    }

    [Fact]
    public void SwitchCamera_ClearsPreviewImage()
    {
        TestInfra.RunSta(() =>
        {
            var vm = CreateVm();
            vm.PreviewImage = TinyImage();
            vm.PreviewCaption = "测试取图 · cam_a（Virtual）";

            vm.Selected = vm.Items.Single(i => i.Id == "cam_b");

            vm.PreviewImage.Should().BeNull();
            vm.PreviewCaption.Should().BeEmpty();
            vm.EditId.Should().Be("cam_b");
        });
    }

    [Fact]
    public void Save_HardwareDeviceId_KeepsSavedMessage_WithoutLiveParamError()
    {
        TestInfra.RunSta(() =>
        {
            _cfg.Cameras =
            [
                new CameraConfig { Id = "cam_basler", Type = "Basler", DeviceId = "" },
            ];
            var vm = CreateVm();
            vm.Selected = vm.Items.Single(i => i.Id == "cam_basler");
            vm.EditDeviceId = "23616873";

            vm.SaveCommand.Execute(null);

            vm.Message.Should().StartWith("已保存 cam_basler");
            vm.Message.Should().NotContain("读取相机参数失败");
            _cfg.Cameras.Single(c => c.Id == "cam_basler").DeviceId.Should().Be("23616873");
            vm.EditDeviceId.Should().Be("23616873");
        });
    }
}
