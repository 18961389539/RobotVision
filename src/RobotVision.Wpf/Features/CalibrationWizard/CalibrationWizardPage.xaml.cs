using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RobotVision.WpfHost.Shared;

namespace RobotVision.WpfHost.Features.CalibrationWizard;

/// <summary>标定向导页：图像点选委托 <see cref="CalibrationWizardImageHost"/>。</summary>
public partial class CalibrationWizardPage : Page
{
    private readonly CalibrationWizardViewModel _vm;
    private CalibrationWizardImageHost? _imageHost;

    public CalibrationWizardPage(CalibrationWizardViewModel viewModel, IDialogService dialogs)
    {
        _vm = viewModel;
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () =>
        {
            _imageHost?.Unwire();
            if (_vm.HasUnsavedChanges)
            {
                dialogs.ConfirmDiscard(
                    $"标定向导已采集 {_vm.CollectedFrames} 帧但尚未保存为标定档案，离开将丢弃。选择「否」可返回继续编辑（若导航已切换，请手动回到本页）。",
                    "未保存修改");
            }
        });
        InitializeComponent();
        NumberBoxCommit.Bind(this, _vm);

        Loaded += (_, _) =>
        {
            _vm.RefreshCameras();
            _imageHost ??= new CalibrationWizardImageHost(_vm, new ImageViewerPickViewport(Viewer));
            _imageHost.Wire();
        };
    }

    private void OnViewerMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _imageHost?.OnMouseLeftButtonDown(e);
}
