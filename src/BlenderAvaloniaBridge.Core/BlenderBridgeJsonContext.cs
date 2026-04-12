using System.Text.Json.Serialization;

namespace BlenderAvaloniaBridge;

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    WriteIndented = false)]
[JsonSerializable(typeof(BlenderBridgeDiagnosticsSnapshot))]
internal partial class BlenderBridgeJsonContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    WriteIndented = true)]
[JsonSerializable(typeof(BlenderBridgeDiagnosticsSnapshot))]
internal partial class BlenderBridgeIndentedJsonContext : JsonSerializerContext
{
}
