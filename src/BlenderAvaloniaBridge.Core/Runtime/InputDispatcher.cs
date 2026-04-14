using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge.Runtime;

internal sealed class InputDispatcher
{
    private readonly IBlenderBridgeStatusSink? _statusSink;
    private readonly MouseInputInjector _mouseInputInjector;

    public InputDispatcher(IBlenderBridgeStatusSink? statusSink)
    {
        _statusSink = statusSink;
        _mouseInputInjector = new MouseInputInjector(statusSink);
    }

    public Task DispatchAsync(Window window, ProtocolEnvelope envelope)
    {
        var point = new Point(envelope.X ?? 0, envelope.Y ?? 0);
        var modifiers = ToModifiers(envelope.Modifiers);

        if (_mouseInputInjector.TryDispatch(window, envelope, point, modifiers))
        {
            return Task.CompletedTask;
        }

        switch (envelope.Type)
        {
            case "key_down":
                {
                    var key = KeyMap.ToKey(envelope.Key);
                    _statusSink?.SetBridgeStatus($"KeyDown {envelope.Key ?? "unknown"}");
                    window.KeyPress(key, modifiers, KeyMap.ToPhysicalKey(envelope.Key), envelope.Text);
                    break;
                }
            case "key_up":
                {
                    var key = KeyMap.ToKey(envelope.Key);
                    _statusSink?.SetBridgeStatus($"KeyUp {envelope.Key ?? "unknown"}");
                    window.KeyRelease(key, modifiers, KeyMap.ToPhysicalKey(envelope.Key), envelope.Text);
                    break;
                }
            case "text":
                _statusSink?.SetBridgeStatus($"Typed: {envelope.Text ?? string.Empty}");
                window.KeyTextInput(envelope.Text ?? string.Empty);
                break;
            case "focus":
                _statusSink?.SetBridgeStatus(envelope.Focus == true ? "Focus gained" : "Focus lost");
                if (envelope.Focus == true)
                {
                    window.Focus();
                }

                break;
        }

        return Task.CompletedTask;
    }

    private static RawInputModifiers ToModifiers(IEnumerable<string>? modifierNames)
    {
        var modifiers = RawInputModifiers.None;
        if (modifierNames is null)
        {
            return modifiers;
        }

        foreach (var modifierName in modifierNames)
        {
            switch (modifierName.ToLowerInvariant())
            {
                case "shift":
                    modifiers |= RawInputModifiers.Shift;
                    break;
                case "ctrl":
                case "control":
                    modifiers |= RawInputModifiers.Control;
                    break;
                case "alt":
                    modifiers |= RawInputModifiers.Alt;
                    break;
            }
        }

        return modifiers;
    }

    private static class KeyMap
    {
        private static readonly Dictionary<string, Key> KeyLookup = new(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = Key.A,
            ["B"] = Key.B,
            ["C"] = Key.C,
            ["D"] = Key.D,
            ["E"] = Key.E,
            ["F"] = Key.F,
            ["G"] = Key.G,
            ["H"] = Key.H,
            ["I"] = Key.I,
            ["J"] = Key.J,
            ["K"] = Key.K,
            ["L"] = Key.L,
            ["M"] = Key.M,
            ["N"] = Key.N,
            ["O"] = Key.O,
            ["P"] = Key.P,
            ["Q"] = Key.Q,
            ["R"] = Key.R,
            ["S"] = Key.S,
            ["T"] = Key.T,
            ["U"] = Key.U,
            ["V"] = Key.V,
            ["W"] = Key.W,
            ["X"] = Key.X,
            ["Y"] = Key.Y,
            ["Z"] = Key.Z,
            ["ZERO"] = Key.D0,
            ["ONE"] = Key.D1,
            ["TWO"] = Key.D2,
            ["THREE"] = Key.D3,
            ["FOUR"] = Key.D4,
            ["FIVE"] = Key.D5,
            ["SIX"] = Key.D6,
            ["SEVEN"] = Key.D7,
            ["EIGHT"] = Key.D8,
            ["NINE"] = Key.D9,
            ["SPACE"] = Key.Space,
            ["TAB"] = Key.Tab,
            ["RET"] = Key.Enter,
            ["NUMPAD_ENTER"] = Key.Enter,
            ["BACK_SPACE"] = Key.Back,
            ["DEL"] = Key.Delete,
            ["ESC"] = Key.Escape,
            ["LEFT_ARROW"] = Key.Left,
            ["RIGHT_ARROW"] = Key.Right,
            ["UP_ARROW"] = Key.Up,
            ["DOWN_ARROW"] = Key.Down,
            ["HOME"] = Key.Home,
            ["END"] = Key.End
        };

        private static readonly Dictionary<string, PhysicalKey> PhysicalKeyLookup = new(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = PhysicalKey.A,
            ["B"] = PhysicalKey.B,
            ["C"] = PhysicalKey.C,
            ["D"] = PhysicalKey.D,
            ["E"] = PhysicalKey.E,
            ["F"] = PhysicalKey.F,
            ["G"] = PhysicalKey.G,
            ["H"] = PhysicalKey.H,
            ["I"] = PhysicalKey.I,
            ["J"] = PhysicalKey.J,
            ["K"] = PhysicalKey.K,
            ["L"] = PhysicalKey.L,
            ["M"] = PhysicalKey.M,
            ["N"] = PhysicalKey.N,
            ["O"] = PhysicalKey.O,
            ["P"] = PhysicalKey.P,
            ["Q"] = PhysicalKey.Q,
            ["R"] = PhysicalKey.R,
            ["S"] = PhysicalKey.S,
            ["T"] = PhysicalKey.T,
            ["U"] = PhysicalKey.U,
            ["V"] = PhysicalKey.V,
            ["W"] = PhysicalKey.W,
            ["X"] = PhysicalKey.X,
            ["Y"] = PhysicalKey.Y,
            ["Z"] = PhysicalKey.Z,
            ["SPACE"] = PhysicalKey.Space,
            ["TAB"] = PhysicalKey.Tab,
            ["RET"] = PhysicalKey.Enter,
            ["BACK_SPACE"] = PhysicalKey.Backspace,
            ["DEL"] = PhysicalKey.Delete,
            ["LEFT_ARROW"] = PhysicalKey.ArrowLeft,
            ["RIGHT_ARROW"] = PhysicalKey.ArrowRight,
            ["UP_ARROW"] = PhysicalKey.ArrowUp,
            ["DOWN_ARROW"] = PhysicalKey.ArrowDown
        };

        public static Key ToKey(string? key) =>
            key is not null && KeyLookup.TryGetValue(key, out var result) ? result : Key.None;

        public static PhysicalKey ToPhysicalKey(string? key) =>
            key is not null && PhysicalKeyLookup.TryGetValue(key, out var result) ? result : PhysicalKey.None;
    }
}
