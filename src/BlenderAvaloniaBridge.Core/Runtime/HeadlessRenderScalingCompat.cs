using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;

namespace BlenderAvaloniaBridge.Runtime;

internal static class HeadlessRenderScalingCompat
{
#if AVALONIA11_COMPAT
    private static readonly Lazy<ReflectionAccess> Reflection = new(CreateReflectionAccess);
#endif

    public static void Apply(Window window, double renderScaling)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (double.IsNaN(renderScaling) || double.IsInfinity(renderScaling) || renderScaling <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renderScaling));
        }

#if AVALONIA11_COMPAT
        ApplyViaReflection(window, renderScaling);
#else
        window.SetRenderScaling(renderScaling);
#endif
    }

#if AVALONIA11_COMPAT
    private static void ApplyViaReflection(Window window, double renderScaling)
    {
        var access = Reflection.Value;
        var impl = access.GetImpl.Invoke(null, [window])
            ?? throw new InvalidOperationException("Failed to resolve the Avalonia headless window implementation.");

        access.RenderScalingField.SetValue(impl, renderScaling);
        if (access.ScalingChangedProperty.GetValue(impl) is Action<double> scalingChanged)
        {
            scalingChanged(renderScaling);
        }

        window.InvalidateMeasure();
        window.InvalidateVisual();
    }

    private static ReflectionAccess CreateReflectionAccess()
    {
        var headlessAssembly = typeof(AvaloniaHeadlessPlatform).Assembly;
        var extensionsType = headlessAssembly.GetType("Avalonia.Headless.HeadlessWindowExtensions")
            ?? throw new InvalidOperationException("Unable to locate Avalonia headless window extensions.");
        var implType = headlessAssembly.GetType("Avalonia.Headless.HeadlessWindowImpl")
            ?? throw new InvalidOperationException("Unable to locate Avalonia headless window implementation type.");
        var getImpl = extensionsType.GetMethod(
            "GetImpl",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [typeof(TopLevel)],
            modifiers: null)
            ?? throw new InvalidOperationException("Unable to locate the Avalonia headless implementation accessor.");
        var renderScalingField = implType.GetField(
            "<RenderScaling>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Unable to locate the Avalonia headless render scaling field.");
        var scalingChangedProperty = implType.GetProperty(
            "ScalingChanged",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Unable to locate the Avalonia headless scaling change callback.");

        return new ReflectionAccess(getImpl, renderScalingField, scalingChangedProperty);
    }

    private sealed record ReflectionAccess(
        MethodInfo GetImpl,
        FieldInfo RenderScalingField,
        PropertyInfo ScalingChangedProperty);
#endif
}
