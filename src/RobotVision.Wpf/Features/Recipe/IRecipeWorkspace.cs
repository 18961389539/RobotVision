using RobotVision.Core.Recipe;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方页协作对象访问编辑器、忙状态与脏标记。由 <see cref="RecipeViewModel"/> 实现。</summary>
internal interface IRecipeWorkspace
{
    RecipeConfig Editor { get; }
    string Message { get; set; }
    bool IsBusy { get; set; }
    bool HasUnsavedChanges { get; }
    bool CanTestTrigger { get; }
    /// <summary>当前编辑对应的磁盘原名；新建/复制时为空。</summary>
    string OriginalName { get; }
    void CommitEdits();
    void NotifyDirty();
    void NotifyEditorMutated();
    /// <summary>测试触发开始：切到结果图视图（有旧图则冻住直到新快照）。</summary>
    void OnTestStarting();
}
