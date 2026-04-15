using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BlenderAvaloniaBridge.Protocol;
using BlenderAvaloniaBridge.Runtime;
using BlenderAvaloniaBridge.Sample.ViewModels;
using BlenderAvaloniaBridge.Sample.ViewModels.Pages;
using BlenderAvaloniaBridge.Sample.Views.Pages;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class InputDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_PointerMoveAfterLeftButtonPress_PreservesLeftButtonState()
    {
        var runtimeThread = HeadlessRuntimeThread.Shared;

        var leftButtonPressedDuringMove = await runtimeThread.InvokeAsync(() =>
        {
            var window = new Window
            {
                Width = 100,
                Height = 100,
                Content = new Border
                {
                    Width = 100,
                    Height = 100
                }
            };

            bool? observed = null;
            window.PointerMoved += (_, e) =>
            {
                observed = e.GetCurrentPoint(window).Properties.IsLeftButtonPressed;
            };

            window.Show();

            var dispatcher = new InputDispatcher(statusSink: null);
            dispatcher.DispatchAsync(
                window,
                new ProtocolEnvelope
                {
                    Type = "pointer_down",
                    X = 10,
                    Y = 10,
                    Button = "left"
                }).GetAwaiter().GetResult();
            dispatcher.DispatchAsync(
                window,
                new ProtocolEnvelope
                {
                    Type = "pointer_move",
                    X = 20,
                    Y = 20
                }).GetAwaiter().GetResult();

            return observed;
        });

        Assert.True(leftButtonPressedDuringMove);
    }

    [Fact]
    public async Task DispatchAsync_PointerUp_RaisesPointerReleasedWithLeftInitialPress()
    {
        var runtimeThread = HeadlessRuntimeThread.Shared;

        var releasedInfo = await runtimeThread.InvokeAsync(() =>
        {
            var window = new Window
            {
                Width = 100,
                Height = 100,
                Content = new Border
                {
                    Width = 100,
                    Height = 100
                }
            };

            bool released = false;
            MouseButton initialPressButton = MouseButton.None;
            bool? leftPressedAtRelease = null;

            window.PointerPressed += (_, e) =>
            {
                e.Pointer.Capture(window);
            };

            window.PointerReleased += (_, e) =>
            {
                released = true;
                initialPressButton = e.InitialPressMouseButton;
                leftPressedAtRelease = e.GetCurrentPoint(window).Properties.IsLeftButtonPressed;
            };

            window.Show();

            var dispatcher = new InputDispatcher(statusSink: null);
            dispatcher.DispatchAsync(
                window,
                new ProtocolEnvelope
                {
                    Type = "pointer_down",
                    X = 20,
                    Y = 20,
                    Button = "left"
                }).GetAwaiter().GetResult();

            dispatcher.DispatchAsync(
                window,
                new ProtocolEnvelope
                {
                    Type = "pointer_up",
                    X = 20,
                    Y = 20,
                    Button = "left"
                }).GetAwaiter().GetResult();

            return (released, initialPressButton, leftPressedAtRelease);
        });

        Assert.True(releasedInfo.released);
        Assert.Equal(MouseButton.Left, releasedInfo.initialPressButton);
        Assert.False(releasedInfo.leftPressedAtRelease);
    }

    [Fact]
    public async Task DispatchAsync_SortableListDragRelease_CompletesReorder()
    {
        var runtimeThread = HeadlessRuntimeThread.Shared;

        var statusText = await runtimeThread.InvokeAsync(() =>
        {
            var viewModel = new SortableListDemoPageViewModel();
            var view = new SortableListDemoPageView
            {
                DataContext = viewModel
            };
            var window = new Window
            {
                Width = 980,
                Height = 760,
                Content = view
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var dragHandle = view.GetVisualDescendants()
                .OfType<Control>()
                .FirstOrDefault(control => Equals(control.Tag, "DragHandle"));
            if (dragHandle is null)
            {
                return "drag-handle-not-found";
            }

            var start = dragHandle.TranslatePoint(
                new Point(dragHandle.Bounds.Width * 0.5, dragHandle.Bounds.Height * 0.5),
                window);
            if (!start.HasValue)
            {
                return "drag-handle-point-not-found";
            }

            var dispatcher = new InputDispatcher(statusSink: null);
            dispatcher.DispatchAsync(
                window,
                new ProtocolEnvelope
                {
                    Type = "pointer_down",
                    X = start.Value.X,
                    Y = start.Value.Y,
                    Button = "left"
                }).GetAwaiter().GetResult();
            dispatcher.DispatchAsync(
                window,
                new ProtocolEnvelope
                {
                    Type = "pointer_move",
                    X = start.Value.X,
                    Y = start.Value.Y + 10
                }).GetAwaiter().GetResult();
            dispatcher.DispatchAsync(
                window,
                new ProtocolEnvelope
                {
                    Type = "pointer_move",
                    X = start.Value.X,
                    Y = start.Value.Y + 120
                }).GetAwaiter().GetResult();
            dispatcher.DispatchAsync(
                window,
                new ProtocolEnvelope
                {
                    Type = "pointer_up",
                    X = start.Value.X,
                    Y = start.Value.Y + 120,
                    Button = "left"
                }).GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs();

            return viewModel.StatusText;
        });

        Assert.StartsWith("Moved \"", statusText, StringComparison.Ordinal);
    }
}
