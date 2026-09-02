using System.Windows;
using System.Windows.Input;
using FluentAssertions;
using RobotVision.Hosting;
using RobotVision.Infrastructure.Calibration;
using RobotVision.Infrastructure.Cameras;
using RobotVision.WpfHost.Features.CalibrationWizard;

namespace RobotVision.Wpf.Tests;

public sealed class CalibrationWizardImageHostTests : IDisposable
{
    private readonly CameraManager _cameras = new();
    private readonly CalibrationManager _calibration = new();

    public CalibrationWizardImageHostTests()
    {
        _cameras.Register(new VirtualCamera("cam_v", 128, 96, "Chessboard"));
    }

    public void Dispose()
    {
        _cameras.Dispose();
        _calibration.Dispose();
    }

    private CalibrationWizardViewModel CreateVm() =>
        new(TestInfra.CameraFacade(_cameras), TestInfra.CalibrationFacade(_calibration),
            TestInfra.CalibrationWizard(TestInfra.CameraFacade(_cameras), TestInfra.CalibrationFacade(_calibration)),
            TestInfra.CreateAppConfig(Path.GetTempPath()),
            new TestDialogService(), TestLog.Null<CalibrationWizardViewModel>());

    [Fact]
    public void Wire_IsIdempotent_AndSyncsMarkers()
    {
        var vm = CreateVm();
        var viewport = new FakePickViewport();
        var host = new CalibrationWizardImageHost(vm, viewport);

        host.Wire();
        host.Wire();
        vm.AddPoint(12, 34);

        viewport.Markers.Should().ContainSingle(m => m.X == 12 && m.Y == 34 && m.Label == "1");
    }

    [Fact]
    public void Unwire_ClearsMarkers()
    {
        var vm = CreateVm();
        var viewport = new FakePickViewport();
        var host = new CalibrationWizardImageHost(vm, viewport);
        host.Wire();
        vm.AddPoint(1, 2);

        host.Unwire();

        viewport.Markers.Should().BeEmpty();
    }

    [Fact]
    public void OnMouseLeftButtonDown_WhenClickable_AddsPoint()
    {
        TestInfra.RunSta(() =>
        {
            var vm = CreateVm();
            vm.Clickable = true;
            var viewport = new FakePickViewport { NextHit = new Point(50, 60) };
            var host = new CalibrationWizardImageHost(vm, viewport);
            host.Wire();

            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            };
            host.OnMouseLeftButtonDown(args);

            args.Handled.Should().BeTrue();
            vm.Points.Should().ContainSingle(p => p.PixelX == 50 && p.PixelY == 60);
            viewport.Markers.Should().ContainSingle(m => m.Label == "1");
        });
    }

    [Fact]
    public void OnMouseLeftButtonDown_WhenNotClickable_IsIgnored()
    {
        TestInfra.RunSta(() =>
        {
            var vm = CreateVm();
            vm.Clickable = false;
            var viewport = new FakePickViewport { NextHit = new Point(1, 2) };
            var host = new CalibrationWizardImageHost(vm, viewport);

            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            };
            host.OnMouseLeftButtonDown(args);

            args.Handled.Should().BeFalse();
            vm.Points.Should().BeEmpty();
        });
    }

    [Fact]
    public void OnMouseLeftButtonDown_WhenMiss_DoesNotAddPoint()
    {
        TestInfra.RunSta(() =>
        {
            var vm = CreateVm();
            vm.Clickable = true;
            var viewport = new FakePickViewport();
            var host = new CalibrationWizardImageHost(vm, viewport);

            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            };
            host.OnMouseLeftButtonDown(args);

            vm.Points.Should().BeEmpty();
        });
    }
}
