using System.Text.Json.Serialization;

namespace BlenderAvaloniaBridge.Bridge;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false)]
[JsonSerializable(typeof(ProtocolEnvelope))]
public partial class ProtocolJsonContext : JsonSerializerContext
{
}
