using RobotVision.Core.Models;
using RobotVision.Core.Recipe;
using RobotVision.Teach;

namespace RobotVision.WpfHost.Features.Recipe;

/// <summary>配方页协作对象访问编辑器、忙状态与脏标记。由 <see cref="RecipeViewModel"/> 实现。</summary>
public interface IRecipeWorkspace
{
    RecipeConfig Editor { get; }
    string Message { get; set; }
    bool IsBusy { get; set; }
    bool HasUnsavedChanges { get; }
    bool CanTestTrigger { get; }
    /// <summary>试触发前置校验失败原因；null = 可试触发。</summary>
    string? TestTriggerBlockReason { get; }
    /// <summary>试触发被禁用时在按钮旁展示原因。</summary>
    bool ShowTestTriggerBlockHint { get; }
    string TestTriggerBlockHint { get; }
    string TestTriggerButtonToolTip { get; }
    /// <summary>当前编辑对应的磁盘原名；新建/复制时为空。</summary>
    string OriginalName { get; }
    void CommitEdits();
    void NotifyDirty();
    void NotifyEditorMutated();
    int RecipeTestTimeoutMs { get; }
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
    /// <summary>精修方法或特征框相对基线已变时确认；取消返回 false。确认后清除已记示教输出。</summary>
    bool ConfirmGrabOriginIfNeeded(string action);
    /// <summary>模板/形状匹配且特征框过扁时确认；取消返回 false。</summary>
    bool ConfirmFlatFeatureRoiIfNeeded(string action);
}
