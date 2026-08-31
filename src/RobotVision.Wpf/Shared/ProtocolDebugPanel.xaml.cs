using System.Windows.Controls;

namespace RobotVision.WpfHost.Shared;

/// <summary>
/// 光源协议调试面板（Network / Serial 参数卡片共用）。纯标记控件：
/// 无代码逻辑，DataContext 由 VisualTree 继承，绑定到 LightingsViewModel 的
/// DebugCommand / SendDebugCommand / DebugResult。
/// </summary>
public partial class ProtocolDebugPanel : UserControl
{
    public ProtocolDebugPanel()
    {
        InitializeComponent();
    }
}
