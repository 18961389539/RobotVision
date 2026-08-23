using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ImageViewer.ViewModels
{
    /// <summary>
    /// 撤销/重做命令接口
    /// Chinese: 定义撤销/重做命令所需的 Execute 与 Undo 方法。
    /// English: Interface for undo/redo commands exposing Execute and Undo methods.
    /// </summary>
    public interface IUndoRedoCommand
    {
        void Execute();
        void Undo();
    }

    /// <summary>
    /// 撤销/重做管理器
    /// Chinese: 简单的栈式撤销/重做管理器，记录执行的命令并支持 Undo/Redo 操作。
    /// English: Simple stack-based undo/redo manager that executes commands and tracks undo/redo stacks.
    /// </summary>
    public class UndoRedoManager : INotifyPropertyChanged
    {
        // 修复：撤销栈容量上限，超出时丢弃最旧记录，避免长期编辑导致内存无界增长。
        private const int MaximumUndoDepth = 100;

        private readonly Stack<IUndoRedoCommand> _undoStack = new Stack<IUndoRedoCommand>();
        private readonly Stack<IUndoRedoCommand> _redoStack = new Stack<IUndoRedoCommand>();

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 执行命令并将其推入撤销栈。
        /// Chinese: 执行给定的 IUndoRedoCommand，然后将其推入撤销栈，同时清空重做栈。
        /// English: Executes the given command, pushes it onto the undo stack and clears the redo stack.
        /// </summary>
        /// <param name="command">要执行的命令 / Command to execute</param>
        public void Execute(IUndoRedoCommand command)
        {
            // 修复：先执行成功再入栈——若 Execute 抛异常（命令部分生效），不写入撤销栈，
            // 避免撤销栈与真实状态不一致；同时清空重做栈的逻辑也仅在成功后才执行。
            command.Execute();
            _undoStack.Push(command);
            TrimUndoStack();
            _redoStack.Clear();
            RaiseStateChanged();
        }

        /// <summary>
        /// 撤销上一个命令（如果存在）。
        /// Chinese: 从撤销栈弹出并调用 Undo，然后将其放入重做栈。
        /// English: Undoes the most recently executed command if available.
        /// </summary>
        public void Undo()
        {
            if (_undoStack.Count > 0)
            {
                var command = _undoStack.Pop();
                command.Undo();
                _redoStack.Push(command);
                RaiseStateChanged();
            }
        }

        /// <summary>
        /// 重做上一个被撤销的命令（如果存在）。
        /// Chinese: 从重做栈弹出并重新执行命令，然后将其放回撤销栈。
        /// English: Redoes the most recently undone command if available.
        /// </summary>
        public void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var command = _redoStack.Pop();
                command.Execute();
                _undoStack.Push(command);
                // 修复：重做同样受容量上限约束。
                TrimUndoStack();
                RaiseStateChanged();
            }
        }

        /// <summary>
        /// 清空撤销与重做栈。
        /// Chinese: 移除所有记录的命令，重置历史。
        /// English: Clears both undo and redo stacks, resetting history.
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            RaiseStateChanged();
        }

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// 撤销栈超上限时丢弃最旧记录（栈底），保留最近 MaximumUndoDepth 条。
        /// </summary>
        private void TrimUndoStack()
        {
            if (_undoStack.Count <= MaximumUndoDepth)
            {
                return;
            }

            IUndoRedoCommand[] mostRecent = _undoStack.ToArray();
            _undoStack.Clear();
            // ToArray 从栈顶（最新）开始，逆序压回保持原始顺序。
            for (int i = mostRecent.Length - 1; i >= 0; i--)
            {
                if (i < MaximumUndoDepth)
                {
                    _undoStack.Push(mostRecent[i]);
                }
            }
        }

        private void RaiseStateChanged()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
