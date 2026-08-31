using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Infrastructure.Inference.Strategies;

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
    /// <summary>
    /// 代码改了 Editor 子对象（POCO、无 INPC）后，通知绑定重读，
    /// 精修方法下拉框等才会跟着切。脏轮询不要走这条，以免打断正在编辑的输入框。
    /// </summary>
    void RefreshEditorBindings();
    /// <summary>结果库健康信号转成的推荐先验；无数据则为 null。不进 TRIGGER。</summary>
    RecipePrior? PlaybookPrior { get; }
    /// <summary>测试触发开始：切到结果图视图（有旧图则冻住直到新快照）。</summary>
    void OnTestStarting();
    /// <summary>把建议特征框写入编辑器并刷新 ROI 绑定。</summary>
    void ApplySuggestedFeatureRoi(Roi roi);
}
