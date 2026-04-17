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
    public static IEnumerable<object[]> ExpandedKeyMappingCases()
    {
        yield return ["PERIOD", Key.OemPeriod, PhysicalKey.Period];
        yield return ["NUMPAD_PERIOD", Key.Decimal, PhysicalKey.NumPadDecimal];
        yield return ["COMMA", Key.OemComma, PhysicalKey.Comma];
        yield return ["MINUS", Key.OemMinus, PhysicalKey.Minus];
        yield return ["PLUS", Key.OemPlus, PhysicalKey.Equal];
        yield return ["EQUAL", Key.OemPlus, PhysicalKey.Equal];
        yield return ["SEMI_COLON", Key.OemSemicolon, PhysicalKey.Semicolon];
        yield return ["QUOTE", Key.OemQuotes, PhysicalKey.Quote];
        yield return ["SLASH", Key.OemQuestion, PhysicalKey.Slash];
        yield return ["BACK_SLASH", Key.OemPipe, PhysicalKey.Backslash];
        yield return ["LEFT_BRACKET", Key.OemOpenBrackets, PhysicalKey.BracketLeft];
        yield return ["RIGHT_BRACKET", Key.OemCloseBrackets, PhysicalKey.BracketRight];
        yield return ["ACCENT_GRAVE", Key.OemTilde, PhysicalKey.Backquote];
        yield return ["LEFT_SHIFT", Key.LeftShift, PhysicalKey.ShiftLeft];
        yield return ["RIGHT_SHIFT", Key.RightShift, PhysicalKey.ShiftRight];
        yield return ["LEFT_CTRL", Key.LeftCtrl, PhysicalKey.ControlLeft];
        yield return ["RIGHT_CTRL", Key.RightCtrl, PhysicalKey.ControlRight];
        yield return ["LEFT_ALT", Key.LeftAlt, PhysicalKey.AltLeft];
        yield return ["RIGHT_ALT", Key.RightAlt, PhysicalKey.AltRight];
        yield return ["OSKEY", Key.LWin, PhysicalKey.MetaLeft];
        yield return ["APP", Key.Apps, PhysicalKey.ContextMenu];
        yield return ["INSERT", Key.Insert, PhysicalKey.Insert];
        yield return ["PAGE_UP", Key.PageUp, PhysicalKey.PageUp];
        yield return ["PAGE_DOWN", Key.PageDown, PhysicalKey.PageDown];
        yield return ["LINE_FEED", Key.LineFeed, PhysicalKey.None];
        yield return ["NUMPAD_SLASH", Key.Divide, PhysicalKey.NumPadDivide];
        yield return ["NUMPAD_ASTERIX", Key.Multiply, PhysicalKey.NumPadMultiply];
        yield return ["NUMPAD_MINUS", Key.Subtract, PhysicalKey.NumPadSubtract];
        yield return ["NUMPAD_PLUS", Key.Add, PhysicalKey.NumPadAdd];

        for (var index = 0; index <= 9; index++)
        {
            yield return
            [
                $"NUMPAD_{index}",
                Enum.Parse<Key>($"NumPad{index}"),
                Enum.Parse<PhysicalKey>($"NumPad{index}")
            ];
        }

        for (var index = 1; index <= 24; index++)
        {
            yield return
            [
                $"F{index}",
                Enum.Parse<Key>($"F{index}"),
                Enum.Parse<PhysicalKey>($"F{index}")
            ];
        }
    }

    [Theory]
    [MemberData(nameof(ExpandedKeyMappingCases))]
    public async Task DispatchAsync_ExpandedKeyDown_RaisesExpectedKeyEvent(
        string bridgeKey,
        Key expectedKey,
        PhysicalKey expectedPhysicalKey)
    {
        var runtimeThread = HeadlessRuntimeThread.Shared;

        var observed = await runtimeThread.InvokeAsync(() =>
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

            (Key key, PhysicalKey physicalKey)? keyDown = null;
            window.KeyDown += (_, e) => keyDown = (e.Key, e.PhysicalKey);

            window.Show();
            window.Focus();
            Dispatcher.UIThread.RunJobs();

            var dispatcher = new InputDispatcher(statusSink: null);
            dispatcher.DispatchAsync(
                window,
                new ProtocolEnvelope
                {
                    Type = "key_down",
                    Key = bridgeKey
                }).GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs();

            return keyDown;
        });

        Assert.True(observed.HasValue);
        Assert.Equal(expectedKey, observed.Value.key);
        Assert.Equal(expectedPhysicalKey, observed.Value.physicalKey);
    }

    [Fact]
    public async Task DispatchAsync_KeyUp_RaisesExpectedKeyRelease()
    {
        var runtimeThread = HeadlessRuntimeThread.Shared;

        var observed = await runtimeThread.InvokeAsync(() =>
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

            (Key key, PhysicalKey physicalKey)? keyUp = null;
            window.KeyUp += (_, e) => keyUp = (e.Key, e.PhysicalKey);

            window.Show();
            window.Focus();
            Dispatcher.UIThread.RunJobs();

            var dispatcher = new InputDispatcher(statusSink: null);
            dispatcher.DispatchAsync(
                window,
                new ProtocolEnvelope
                {
                    Type = "key_up",
                    Key = "LEFT_CTRL"
                }).GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs();

            return keyUp;
        });

        Assert.True(observed.HasValue);
        Assert.Equal(Key.LeftCtrl, observed.Value.key);
        Assert.Equal(PhysicalKey.ControlLeft, observed.Value.physicalKey);
    }

    [Fact]
    public async Task DispatchAsync_Text_RaisesTextInput()
    {
        var runtimeThread = HeadlessRuntimeThread.Shared;

        var observed = await runtimeThread.InvokeAsync(() =>
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

            string? text = null;
            window.TextInput += (_, e) => text = e.Text;

            window.Show();
            window.Focus();
            Dispatcher.UIThread.RunJobs();

            var dispatcher = new InputDispatcher(statusSink: null);
            dispatcher.DispatchAsync(
                window,
                new ProtocolEnvelope
                {
                    Type = "text",
                    Text = "."
                }).GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs();

            return text;
        });

        Assert.Equal(".", observed);
    }

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
