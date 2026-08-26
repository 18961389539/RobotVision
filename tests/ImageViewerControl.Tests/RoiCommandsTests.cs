using System.Windows;
using FluentAssertions;
using ImageViewer.Models;
using ImageViewer.ViewModels;

namespace ImageViewerControl.Tests;

/// <summary>
/// ROI 命令测试：RoiStateCommand（完整状态替换）与 RoiLabelCommand（标签变更）的
/// Execute/Undo 语义；与 UndoRedoManager 组合的完整撤销链。
/// </summary>
public class RoiCommandsTests
{
    [Fact]
    public void RoiStateCommand_ExecuteAppliesNew_UndoRestoresOld()
    {
        var roi = new CircleRoi { Center = new Point(0, 0), Radius = 10 };
        var oldState = new CircleRoi { Center = new Point(0, 0), Radius = 10 };
        var newState = new CircleRoi { Center = new Point(50, 60), Radius = 25 };

        var command = new RoiStateCommand(roi, oldState, newState);
        command.Execute();
        roi.Center.Should().Be(new Point(50, 60));
        roi.Radius.Should().Be(25);

        command.Undo();
        roi.Center.Should().Be(new Point(0, 0));
        roi.Radius.Should().Be(10);
    }

    [Fact]
    public void RoiLabelCommand_ExecuteAndUndo_ChangeLabel()
    {
        var roi = new CircleRoi { Label = "旧标签" };

        var command = new RoiLabelCommand(roi, "新标签");
        command.Execute();
        roi.Label.Should().Be("新标签");

        command.Undo();
        roi.Label.Should().Be("旧标签");
    }

    [Fact]
    public void UndoRedoManager_WithRoiStateCommand_RestoresFullChain()
    {
        var manager = new UndoRedoManager();
        var roi = new CircleRoi { Center = new Point(0, 0), Radius = 5 };
        var s0 = new CircleRoi { Center = new Point(0, 0), Radius = 5 };
        var s1 = new CircleRoi { Center = new Point(10, 10), Radius = 5 };
        var s2 = new CircleRoi { Center = new Point(10, 10), Radius = 20 };

        manager.Execute(new RoiStateCommand(roi, s0, s1));
        manager.Execute(new RoiStateCommand(roi, s1, s2));

        roi.Center.Should().Be(new Point(10, 10));
        roi.Radius.Should().Be(20);

        manager.Undo();
        roi.Center.Should().Be(new Point(10, 10)); // 恢复到 s1
        roi.Radius.Should().Be(5);

        manager.Undo();
        roi.Center.Should().Be(new Point(0, 0)); // 恢复到 s0
        roi.Radius.Should().Be(5);

        manager.Redo();
        roi.Radius.Should().Be(5); // s1

        manager.Redo();
        roi.Radius.Should().Be(20); // s2
    }

    [Fact]
    public void RoiStateCommand_ExecuteTwice_AppliesNewEachTime()
    {
        var roi = new CircleRoi { Center = new Point(1, 1), Radius = 2 };
        var newState = new CircleRoi { Center = new Point(9, 9), Radius = 9 };

        var command = new RoiStateCommand(roi, new CircleRoi(), newState);
        command.Execute();
        command.Execute();

        roi.Center.Should().Be(new Point(9, 9));
    }
}
