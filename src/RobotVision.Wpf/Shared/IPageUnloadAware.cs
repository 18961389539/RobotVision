namespace RobotVision.WpfHost.Shared;

/// <summary>页面 Unloaded 时由 <see cref="ViewModelPageLifetime"/> 调用：取消在途任务并等待结束，再释放资源。</summary>
public interface IPageUnloadAware
{
    void OnPageUnloading();
}
