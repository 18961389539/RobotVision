using FluentAssertions;
using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Lighting;
using RobotVision.Teach;
using RobotVision.WpfHost.Features.Recipe;

namespace RobotVision.Wpf.Tests;

public sealed class RecipeLightingEditorTests
{
    [Fact]
    public void EnableLighting_CreatesBothDongguanChannels_AndPrefersDongguanId()
    {
        var manager = new LightingManager();
        manager.Register(new NoopLightController("other"));
        manager.Register(new NoopLightController("dongguan"));
        var workspace = new StubWorkspace();
        var editor = new RecipeLightingEditor(workspace, TestInfra.LightingFacade(manager));

        editor.UseLighting = true;

        editor.SelectedLightControllerId.Should().Be("dongguan");
        workspace.Editor.Lighting!.Channels.Should().HaveCount(2);
        editor.LightBrightness1.Should().Be(128);
        editor.LightBrightness2.Should().Be(128);
        workspace.Editor.Lighting.StabilizeDelayMs.Should().Be(20);
        workspace.Dirty.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SetBrightness2_OnSingleChannelRecipe_AppendsChannel2()
    {
        var manager = new LightingManager();
        manager.Register(new NoopLightController("dongguan"));
        var workspace = new StubWorkspace();
        workspace.Editor.LightControllerId = "dongguan";
        workspace.Editor.Lighting = new LightingConfig
        {
            Channels = [new LightingChannelConfig { Channel = 1, Brightness = 200 }],
        };
        var editor = new RecipeLightingEditor(workspace, TestInfra.LightingFacade(manager));

        editor.LightBrightness2.Should().Be(0);
        editor.LightBrightness2 = 90;

        workspace.Editor.Lighting!.Channels.Should().HaveCount(2);
        workspace.Editor.Lighting.Channels[1].Channel.Should().Be(2);
        workspace.Editor.Lighting.Channels[1].Brightness.Should().Be(90);
        editor.LightBrightness1.Should().Be(200);
    }

    private sealed class StubWorkspace : IRecipeWorkspace
    {
        public int Dirty { get; private set; }

        public RecipeConfig Editor { get; } = new() { Name = "R", CameraId = "cam" };

        public string Message { get; set; } = "";

        public bool IsBusy { get; set; }

        public bool HasUnsavedChanges => Dirty > 0;

        public bool CanTestTrigger => false;

        public string? TestTriggerBlockReason => null;

        public bool ShowTestTriggerBlockHint => false;

        public string TestTriggerBlockHint => "";

        public string TestTriggerButtonToolTip => "";

        public string OriginalName => "";

        public int RecipeTestTimeoutMs => 1000;

        public RecipePrior? PlaybookPrior => null;

        public bool IsPipelineOccupied => false;

        public void CommitEdits() { }

        public void NotifyDirty() => Dirty++;

        public void NotifyEditorMutated() { }

        public void RefreshEditorBindings() { }

        public void OnTestStarting() { }

        public void ApplySuggestedFeatureRoi(Roi roi) { }

        public bool ConfirmGrabOriginIfNeeded(string action) => true;
    }
}
