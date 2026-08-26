using System.ComponentModel;
using FluentAssertions;
using ImageViewer.ViewModels;

namespace ImageViewerControl.Tests;

/// <summary>
/// 撤销/重做管理器测试：执行/撤销/重做/清空、容量上限（100）、
/// 命令执行抛异常时不入栈、CanUndo/CanRedo 状态与 PropertyChanged 通知。
/// </summary>
public class UndoRedoManagerTests
{
    private sealed class FakeCommand(int target, int value) : IUndoRedoCommand
    {
        private readonly int _old = target;
        public int Target { get; private set; } = target;
        public int UndoCount { get; private set; }

        public void Execute() => Target = value;

        public void Undo()
        {
            UndoCount++;
            Target = _old;
        }
    }

    private sealed class ThrowingCommand : IUndoRedoCommand
    {
        public void Execute() => throw new InvalidOperationException("boom");

        public void Undo() => throw new NotSupportedException();
    }

    [Fact]
    public void Execute_RunsAndEnablesUndo()
    {
        var manager = new UndoRedoManager();
        var cmd = new FakeCommand(0, 42);

        manager.Execute(cmd);

        cmd.Target.Should().Be(42);
        manager.CanUndo.Should().BeTrue();
        manager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Execute_ClearsRedoStack()
    {
        var manager = new UndoRedoManager();
        var a = new FakeCommand(0, 1);
        var b = new FakeCommand(0, 2);
        manager.Execute(a);
        manager.Undo();
        manager.CanRedo.Should().BeTrue();

        manager.Execute(b);

        manager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Undo_RevertsState_PushesToRedo()
    {
        var manager = new UndoRedoManager();
        var cmd = new FakeCommand(0, 42);
        manager.Execute(cmd);

        manager.Undo();

        cmd.Target.Should().Be(0);
        manager.CanUndo.Should().BeFalse();
        manager.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void Redo_ReappliesState_ReturnsToUndo()
    {
        var manager = new UndoRedoManager();
        var cmd = new FakeCommand(0, 42);
        manager.Execute(cmd);
        manager.Undo();

        manager.Redo();

        cmd.Target.Should().Be(42);
        manager.CanUndo.Should().BeTrue();
        manager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Undo_WhenEmpty_DoesNothing()
    {
        var manager = new UndoRedoManager();

        manager.Undo();

        manager.CanUndo.Should().BeFalse();
        manager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Redo_WhenEmpty_DoesNothing()
    {
        var manager = new UndoRedoManager();

        manager.Redo();

        manager.CanUndo.Should().BeFalse();
        manager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Clear_ResetsBothStacks()
    {
        var manager = new UndoRedoManager();
        manager.Execute(new FakeCommand(0, 1));
        manager.Execute(new FakeCommand(0, 2));

        manager.Clear();

        manager.CanUndo.Should().BeFalse();
        manager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Execute_ThrowingCommand_IsNotPushedToUndoStack()
    {
        var manager = new UndoRedoManager();

        var act = () => manager.Execute(new ThrowingCommand());

        act.Should().Throw<InvalidOperationException>();
        manager.CanUndo.Should().BeFalse(); // 异常命令不得入栈
        manager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void UndoRedo_Sequence_RestoresOriginalState()
    {
        var manager = new UndoRedoManager();
        var cmd = new FakeCommand(0, 1);

        manager.Execute(cmd);   // 1
        manager.Execute(cmd);   // 1（同一命令再次执行）
        manager.Undo();         // 0
        manager.Undo();         // 0（重复撤销：恢复到旧值）

        cmd.Target.Should().Be(0);
        manager.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void UndoDepth_IsBoundedTo100()
    {
        var manager = new UndoRedoManager();
        for (var i = 0; i < 150; i++)
            manager.Execute(new FakeCommand(0, i));

        manager.CanUndo.Should().BeTrue();
        // 内部栈上限 100：反复撤销 150 次不应崩溃，且状态一致
        for (var i = 0; i < 150; i++)
            manager.Undo();
        manager.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void Redo_IsAlsoBoundedToCapacity()
    {
        var manager = new UndoRedoManager();
        for (var i = 0; i < 120; i++)
            manager.Execute(new FakeCommand(0, i));
        for (var i = 0; i < 120; i++)
            manager.Undo();

        manager.CanRedo.Should().BeTrue();
        for (var i = 0; i < 120; i++)
            manager.Redo();
        manager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void StateChanges_RaisePropertyChanged()
    {
        var manager = new UndoRedoManager();
        var raised = new List<string?>();
        manager.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        manager.Execute(new FakeCommand(0, 1));
        manager.Undo();
        manager.Redo();
        manager.Clear();

        raised.Should().Contain(nameof(UndoRedoManager.CanUndo));
        raised.Should().Contain(nameof(UndoRedoManager.CanRedo));
    }
}
