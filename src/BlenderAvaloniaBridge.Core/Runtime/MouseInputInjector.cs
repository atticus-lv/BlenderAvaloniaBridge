using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge.Runtime;

internal sealed class MouseInputInjector
{
    private readonly IBlenderBridgeStatusSink? _statusSink;
    private RawInputModifiers _pressedButtons;

    public MouseInputInjector(IBlenderBridgeStatusSink? statusSink)
    {
        _statusSink = statusSink;
    }

    public bool TryDispatch(Window window, ProtocolEnvelope envelope, Point point, RawInputModifiers modifiers)
    {
        switch (envelope.Type)
        {
            case "pointer_move":
                {
                    var moveModifiers = modifiers | _pressedButtons;
                    _statusSink?.SetBridgeStatus($"MouseMove {(int)point.X},{(int)point.Y}");
                    window.MouseMove(point, moveModifiers);
                    return true;
                }
            case "pointer_down":
                {
                    var button = ToMouseButton(envelope.Button);
                    _pressedButtons |= ToButtonModifier(button);
                    _statusSink?.SetBridgeStatus($"PointerDown {envelope.Button ?? "unknown"} {(int)point.X},{(int)point.Y}");
                    window.MouseDown(point, button, modifiers | _pressedButtons);
                    return true;
                }
            case "pointer_up":
                {
                    var button = ToMouseButton(envelope.Button);
                    _pressedButtons &= ~ToButtonModifier(button);
                    var releaseModifiers = modifiers | _pressedButtons;
                    _statusSink?.SetBridgeStatus($"PointerUp {envelope.Button ?? "unknown"} {(int)point.X},{(int)point.Y}");
                    window.MouseUp(point, button, releaseModifiers);
                    return true;
                }
            case "wheel":
                _statusSink?.SetBridgeStatus($"Wheel {envelope.DeltaX ?? 0:0.##},{envelope.DeltaY ?? 0:0.##}");
                window.MouseWheel(point, new Vector(envelope.DeltaX ?? 0, envelope.DeltaY ?? 0), modifiers | _pressedButtons);
                return true;
            default:
                return false;
        }
    }

    private static MouseButton ToMouseButton(string? button) =>
        button?.ToLowerInvariant() switch
        {
            "left" => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            "xbutton1" => MouseButton.XButton1,
            "xbutton2" => MouseButton.XButton2,
            _ => MouseButton.None
        };

    private static RawInputModifiers ToButtonModifier(MouseButton button) =>
        button switch
        {
            MouseButton.Left => RawInputModifiers.LeftMouseButton,
            MouseButton.Right => RawInputModifiers.RightMouseButton,
            MouseButton.Middle => RawInputModifiers.MiddleMouseButton,
            MouseButton.XButton1 => RawInputModifiers.XButton1MouseButton,
            MouseButton.XButton2 => RawInputModifiers.XButton2MouseButton,
            _ => RawInputModifiers.None
        };
}
