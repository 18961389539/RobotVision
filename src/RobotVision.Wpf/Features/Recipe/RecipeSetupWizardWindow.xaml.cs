using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace RobotVision.WpfHost.Features.Recipe;

public partial class RecipeSetupWizardWindow : FluentWindow
{
    public RecipeSetupWizardWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        UpdateFeatureOverlay();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RecipeSetupWizardViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        HookViewModel(e.NewValue as RecipeSetupWizardViewModel);
    }

    private void HookViewModel(RecipeSetupWizardViewModel? vm)
    {
        if (vm is null)
            return;
        vm.RequestClose += OnRequestClose;
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is RecipeSetupWizardViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.ReleasePreview();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RecipeSetupWizardViewModel.FeatureOverlayRoi)
            or nameof(RecipeSetupWizardViewModel.Preview)
            or nameof(RecipeSetupWizardViewModel.ShowPreviewPane))
            UpdateFeatureOverlay();
    }

    private void OnPreviewHostSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateFeatureOverlay();

    private void UpdateFeatureOverlay()
    {
        if (FeatureOverlay is null || PreviewImage is null)
            return;
        if (DataContext is not RecipeSetupWizardViewModel vm ||
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
