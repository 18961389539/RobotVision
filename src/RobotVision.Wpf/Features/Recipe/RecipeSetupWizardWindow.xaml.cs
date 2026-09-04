using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using RobotVision.Core.Recipe;
using RobotVision.WpfHost.Shared;
using Wpf.Ui.Controls;

namespace RobotVision.WpfHost.Features.Recipe;

public partial class RecipeSetupWizardWindow : FluentWindow, IDisposable
{
    private RecipeSetupWizardViewModel? _vm;
    private RecipeWizardImageHost? _imageHost;

    public RecipeSetupWizardWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateFeatureOverlay();
        WireImageHost();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UnwireImageHost();
        if (e.OldValue is RecipeSetupWizardViewModel oldVm)
            UnhookViewModel(oldVm);

        _vm = e.NewValue as RecipeSetupWizardViewModel;
        if (_vm is not null)
        {
            HookViewModel(_vm);
            NumberBoxCommit.Bind(this, _vm);
        }
        WireImageHost();
    }

    private void HookViewModel(RecipeSetupWizardViewModel vm)
    {
        vm.RequestClose += OnRequestClose;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.PropertyChanged += OnWizardStepChanged;
        vm.RequestBeginDetectionRoiDraw += OnBeginDetectionRoiDraw;
        vm.RequestBeginFeatureRoiDraw += OnBeginFeatureRoiDraw;
    }

    private void UnhookViewModel(RecipeSetupWizardViewModel vm)
    {
        vm.RequestClose -= OnRequestClose;
        vm.PropertyChanged -= OnViewModelPropertyChanged;
        vm.PropertyChanged -= OnWizardStepChanged;
        vm.RequestBeginDetectionRoiDraw -= OnBeginDetectionRoiDraw;
        vm.RequestBeginFeatureRoiDraw -= OnBeginFeatureRoiDraw;
    }

    private void WireImageHost()
    {
        if (_vm is null || _imageHost is not null)
            return;

        _imageHost = new RecipeWizardImageHost(
            WizardViewer,
            _vm.Workspace,
            _vm.Roi,
            () => TemplateOptions.UsesFeatureTeachRoi(_vm.Workspace.Editor.Template.RefineMethod));
        _imageHost.Wire();
        SyncInteractiveViewer();
    }

    private void UnwireImageHost()
    {
        _imageHost?.Dispose();
        _imageHost = null;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is RecipeSetupWizardViewModel vm)
        {
            UnhookViewModel(vm);
            vm.Dispose();
        }

        Dispose();
    }

    public void Dispose()
    {
        UnwireImageHost();
        GC.SuppressFinalize(this);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RecipeSetupWizardViewModel.FeatureOverlayRoi)
            or nameof(RecipeSetupWizardViewModel.Preview)
            or nameof(RecipeSetupWizardViewModel.ShowPreviewPane)
            or nameof(RecipeSetupWizardViewModel.ShowStaticPreview))
            UpdateFeatureOverlay();
    }

    private void OnWizardStepChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RecipeSetupWizardViewModel.ShowInteractiveViewer)
            or nameof(RecipeSetupWizardViewModel.ViewerImage)
            or nameof(RecipeSetupWizardViewModel.Step))
            SyncInteractiveViewer();
    }

    private void SyncInteractiveViewer()
    {
        if (_vm?.ShowInteractiveViewer == true)
            _imageHost?.SyncFromRecipe();
    }

    private void OnBeginDetectionRoiDraw() => _imageHost?.BeginDetectionRoiDraw();

    private void OnBeginFeatureRoiDraw() => _imageHost?.BeginFeatureRoiDraw();

    private void OnPreviewHostSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateFeatureOverlay();

    private void UpdateFeatureOverlay()
    {
        if (FeatureOverlay is null || PreviewImage is null)
            return;
        if (DataContext is not RecipeSetupWizardViewModel vm ||
            !vm.ShowStaticPreview ||
            vm.FeatureOverlayRoi is not { } roi ||
            PreviewImage.Source is not BitmapSource bmp ||
            bmp.PixelWidth < 1 || bmp.PixelHeight < 1 ||
            PreviewImage.ActualWidth < 1 || PreviewImage.ActualHeight < 1)
        {
            FeatureOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        var viewW = PreviewImage.ActualWidth;
        var viewH = PreviewImage.ActualHeight;
        var scale = Math.Min(viewW / bmp.PixelWidth, viewH / bmp.PixelHeight);
        var dispW = bmp.PixelWidth * scale;
        var dispH = bmp.PixelHeight * scale;
        var offX = (viewW - dispW) / 2.0;
        var offY = (viewH - dispH) / 2.0;
        FeatureOverlay.Width = Math.Max(2, roi.Width * dispW);
        FeatureOverlay.Height = Math.Max(2, roi.Height * dispH);
        FeatureOverlay.Margin = new Thickness(
            offX + roi.X * dispW,
            offY + roi.Y * dispH,
            0, 0);
        FeatureOverlay.Visibility = Visibility.Visible;
    }

    private void OnRequestClose()
    {
        try
        {
            if (DataContext is RecipeSetupWizardViewModel { Applied: true })
                DialogResult = true;
        }
        catch (InvalidOperationException)
        {
            // 非 ShowDialog 打开时不能设 DialogResult
        }

        Close();
    }

    private void OnSuppressBringIntoView(object sender, RequestBringIntoViewEventArgs e) =>
        e.Handled = true;
}
