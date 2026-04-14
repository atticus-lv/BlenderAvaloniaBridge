using System.Globalization;

namespace BlenderAvaloniaBridge;

public enum LaunchMode
{
    DesktopWindow,
    BlenderBridge,
}

public sealed record CommandLineOptions(
    LaunchMode Mode,
    string Host,
    int Port,
    int Width,
    int Height,
    double RenderScaling,
    int TargetFps,
    int IdleHeartbeatFps,
    int ContinuousFrameWindowMs,
    bool UseSharedMemory,
    bool EnableDiagnostics)
{
    public string[] AppArgs { get; init; } = [];

    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var appArgs = new List<string>();

        for (var index = 0; index < args.Count; index++)
        {
            if (!TryMapBridgeArgument(args, index, out var key, out var value, out var consumed))
            {
                appArgs.Add(args[index]);
                continue;
            }

            values[key] = value;
            index += consumed - 1;
        }

        var port = int.TryParse(values.GetValueOrDefault("port", "0"), out var parsedPort) ? parsedPort : 0;
        var forceBridge = IsTrue(values.GetValueOrDefault("bridge"));
        var mode = forceBridge ? LaunchMode.BlenderBridge : LaunchMode.DesktopWindow;
        return new CommandLineOptions(
            Mode: mode,
            Host: values.GetValueOrDefault("host", "127.0.0.1"),
            Port: port,
            Width: int.TryParse(values.GetValueOrDefault("width", "800"), out var width) ? width : 800,
            Height: int.TryParse(values.GetValueOrDefault("height", "600"), out var height) ? height : 600,
            RenderScaling: TryParseDoubleInvariant(values.GetValueOrDefault("render-scaling"), 1.25),
            TargetFps: int.TryParse(values.GetValueOrDefault("target-fps", "60"), out var targetFps) ? targetFps : 60,
            IdleHeartbeatFps: int.TryParse(values.GetValueOrDefault("idle-heartbeat-fps", "4"), out var idleHeartbeatFps) ? idleHeartbeatFps : 4,
            ContinuousFrameWindowMs: int.TryParse(values.GetValueOrDefault("continuous-frame-window-ms", "1000"), out var continuousFrameWindowMs) ? continuousFrameWindowMs : 1000,
            UseSharedMemory: !string.Equals(values.GetValueOrDefault("shared-memory", "true"), "false", StringComparison.OrdinalIgnoreCase),
            EnableDiagnostics: !string.Equals(values.GetValueOrDefault("diagnostics", "true"), "false", StringComparison.OrdinalIgnoreCase))
        {
            AppArgs = [.. appArgs],
        };
    }

    public BlenderBridgeOptions ToBridgeOptions()
    {
        return new BlenderBridgeOptions
        {
            Host = Host,
            Port = Port,
            Width = Width,
            Height = Height,
            RenderScaling = RenderScaling,
            TargetFps = TargetFps,
            IdleHeartbeatFps = IdleHeartbeatFps,
            ContinuousFrameWindowMs = ContinuousFrameWindowMs,
            UseSharedMemory = UseSharedMemory,
            EnableDiagnostics = EnableDiagnostics,
        };
    }

    private static bool TryMapBridgeArgument(
        IReadOnlyList<string> args,
        int index,
        out string key,
        out string value,
        out int consumed)
    {
        key = string.Empty;
        value = string.Empty;
        consumed = 0;

        if (index + 1 >= args.Count)
        {
            return false;
        }

        key = args[index] switch
        {
            "--blender-bridge" => "bridge",
            "--blender-bridge-host" => "host",
            "--blender-bridge-port" => "port",
            "--blender-bridge-width" => "width",
            "--blender-bridge-height" => "height",
            "--blender-bridge-render-scaling" => "render-scaling",
            "--blender-bridge-target-fps" => "target-fps",
            "--blender-bridge-idle-heartbeat-fps" => "idle-heartbeat-fps",
            "--blender-bridge-continuous-frame-window-ms" => "continuous-frame-window-ms",
            "--blender-bridge-shared-memory" => "shared-memory",
            "--blender-bridge-diagnostics" => "diagnostics",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        value = args[index + 1];
        consumed = 2;
        return true;
    }

    private static bool IsTrue(string? value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static double TryParseDoubleInvariant(string? value, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }
}
