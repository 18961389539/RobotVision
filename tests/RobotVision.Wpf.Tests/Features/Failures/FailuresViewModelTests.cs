using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using RobotVision.Hosting;
using RobotVision.WpfHost.Features.Failures;

namespace RobotVision.Wpf.Tests;

/// <summary>
/// 失败现场画廊测试：加载（目录不存在/空/有留存）、筛选选项收集、
/// 配方/错误码筛选、筛选文案与清空按钮文案。
/// WPF 位图解码要求 STA，整个流程在 STA 线程内执行。
/// </summary>
public class FailuresViewModelTests
{
    [Fact]
    public void Refresh_WhenFolderMissing_ShowsHint()
    {
        TestInfra.RunSta(() =>
        {
            using var dir = new TestInfra.TempDir("rv_fail_missing");
            var store = new FailureImageStore(
                new FailureImageConfig { Folder = System.IO.Path.Combine(dir.Path, "no_failures") },
                NullLogger<FailureImageStore>.Instance);

            var vm = new FailuresViewModel(store);
            vm.RefreshAsync().GetAwaiter().GetResult();

            vm.Items.Should().BeEmpty();
            vm.Message.Should().Contain("暂无失败留存");
            vm.FilterSummary.Should().BeEmpty();
        });
    }

    [Fact]
    public void Refresh_WithRetentions_CollectsFiltersAndOrdersDescending()
    {
        TestInfra.RunSta(() =>
        {
            using var dir = new TestInfra.TempDir("rv_fail_load");
            var failFolder = dir.CreateSub("failures");
            WriteRetention(failFolder, "20260825120000000_A01_1005", "A01", "1005");
            WriteRetention(failFolder, "20260825110000000_A01_1005", "A01", "1005");
            WriteRetention(failFolder, "20260825100000000_B02_1099", "B02", "1099");

            var store = new FailureImageStore(
                new FailureImageConfig { Folder = failFolder },
                NullLogger<FailureImageStore>.Instance);
            var vm = new FailuresViewModel(store);
            vm.RefreshAsync().GetAwaiter().GetResult();

            vm.Items.Should().HaveCount(3);
            // 按文件名倒序（时间倒序）
            vm.Items[0].DisplayName.Should().StartWith("20260825120000000");
            vm.Items[2].DisplayName.Should().StartWith("20260825100000000");

            vm.RecipeFilters.Should().Equal("全部", "A01", "B02");
            vm.ErrorCodeFilters.Should().Equal("全部", "1005", "1099");
            vm.FilterSummary.Should().BeEmpty(); // 未筛选不显示摘要
            vm.DeleteAllButtonText.Should().Be("清空全部");
            vm.Message.Should().Contain("共 3 条失败现场");
        });
    }

    [Fact]
    public void RecipeFilter_FiltersItemsAndUpdatesSummary()
    {
        TestInfra.RunSta(() =>
        {
            using var dir = new TestInfra.TempDir("rv_fail_recipe_filter");
            var failFolder = dir.CreateSub("failures");
            WriteRetention(failFolder, "20260825120000000_A01_1005", "A01", "1005");
            WriteRetention(failFolder, "20260825110000000_B02_1007", "B02", "1007");
            WriteRetention(failFolder, "20260825100000000_A01_1099", "A01", "1099");

            var store = new FailureImageStore(
                new FailureImageConfig { Folder = failFolder },
                NullLogger<FailureImageStore>.Instance);
            var vm = new FailuresViewModel(store);
            vm.RefreshAsync().GetAwaiter().GetResult();

            vm.RecipeFilter = "A01";
            vm.Items.Should().HaveCount(2);
            vm.Items.Should().OnlyContain(i => i.Recipe == "A01");
            vm.FilterSummary.Should().Be("筛选: 配方 A01 · 错误码 全部 → 2/3 条");
            vm.DeleteAllButtonText.Should().Be("清空筛选结果");
            vm.Selected.Should().NotBeNull();
        });
    }

    [Fact]
    public void CombinedFilter_RecipeAndErrorCode()
    {
        TestInfra.RunSta(() =>
        {
            using var dir = new TestInfra.TempDir("rv_fail_comb");
            var failFolder = dir.CreateSub("failures");
            WriteRetention(failFolder, "20260825120000000_A01_1005", "A01", "1005");
            WriteRetention(failFolder, "20260825110000000_A01_1007", "A01", "1007");
            WriteRetention(failFolder, "20260825100000000_B02_1005", "B02", "1005");

            var store = new FailureImageStore(
                new FailureImageConfig { Folder = failFolder },
                NullLogger<FailureImageStore>.Instance);
            var vm = new FailuresViewModel(store);
            vm.RefreshAsync().GetAwaiter().GetResult();

            vm.RecipeFilter = "A01";
            vm.ErrorCodeFilter = "1007";

            vm.Items.Should().ContainSingle();
            vm.Items[0].Recipe.Should().Be("A01");
            vm.Items[0].ErrorCode.Should().Be("1007");
            vm.FilterSummary.Should().Be("筛选: 配方 A01 · 错误码 1007 → 1/3 条");
        });
    }

    [Fact]
    public void Filter_ThatMatchesNothing_ShowsEmptyItems()
    {
        TestInfra.RunSta(() =>
        {
            using var dir = new TestInfra.TempDir("rv_fail_empty");
            var failFolder = dir.CreateSub("failures");
            WriteRetention(failFolder, "20260825120000000_A01_1005", "A01", "1005");

            var store = new FailureImageStore(
                new FailureImageConfig { Folder = failFolder },
                NullLogger<FailureImageStore>.Instance);
            var vm = new FailuresViewModel(store);
            vm.RefreshAsync().GetAwaiter().GetResult();

            vm.RecipeFilter = "NOPE";
            vm.Items.Should().BeEmpty();
            vm.Selected.Should().BeNull();
            vm.FilterSummary.Should().Contain("0/1");
        });
    }

    [Fact]
    public void Refresh_WhenJsonMissing_StillListsPng()
    {
        TestInfra.RunSta(() =>
        {
            using var dir = new TestInfra.TempDir("rv_fail_nojson");
            var failFolder = dir.CreateSub("failures");
            // 只有 PNG 没有 JSON：ReadMeta 返回占位值，条目仍应列出
            using (var img = new Mat(32, 32, MatType.CV_8UC3, Scalar.All(50)))
                Cv2.ImWrite(System.IO.Path.Combine(failFolder, "20260825120000000_A01_1005.png"), img);

            var store = new FailureImageStore(
                new FailureImageConfig { Folder = failFolder },
                NullLogger<FailureImageStore>.Instance);
            var vm = new FailuresViewModel(store);
            vm.RefreshAsync().GetAwaiter().GetResult();

            vm.Items.Should().ContainSingle();
            vm.Items[0].Recipe.Should().Be("");
            // 无 Recipe 的条目不进入配方筛选选项
            vm.RecipeFilters.Should().Equal("全部");
        });
    }

    private static void WriteRetention(string folder, string namePrefix, string recipe, string code)
    {
        using var img = new Mat(32, 32, MatType.CV_8UC3, Scalar.All(120));
        Cv2.ImWrite(System.IO.Path.Combine(folder, $"{namePrefix}.png"), img);
        File.WriteAllText(System.IO.Path.Combine(folder, $"{namePrefix}.json"),
            $$"""{"Recipe": "{{recipe}}", "ErrorCode": "{{code}}", "Message": "test", "ElapsedMs": 12}""");
    }
}
