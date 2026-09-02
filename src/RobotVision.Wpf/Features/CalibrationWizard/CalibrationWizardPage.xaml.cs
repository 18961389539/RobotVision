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

    public CalibrationWizardPage(CalibrationWizardViewModel viewModel)
    {
        _vm = viewModel;
        ViewModelPageLifetime.Attach(this, viewModel, onUnloading: () =>
        {
            _imageHost?.Unwire();
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
