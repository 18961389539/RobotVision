using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RobotVision.WpfHost.Features.Monitor;

public partial class MonitorLogPanel : UserControl
{
    public MonitorLogPanel() => InitializeComponent();

    public ListBox LogListControl => LogList;

    private void OnLogListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source
            || ItemsControl.ContainerFromElement(LogList, source) is not ListBoxItem)
            return;
        if (LogList.SelectedItem is not LogLine line)
            return;
        try
        {
            Clipboard.SetText(line.ClipboardText);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("[Monitor] 复制日志到剪贴板失败: {0}", ex.Message);
        }
    }
}
