using System.Text.Json.Serialization;
using System.Text.Json;

namespace BlenderAvaloniaBridge.Protocol;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false)]
[JsonSerializable(typeof(ProtocolEnvelope))]
[JsonSerializable(typeof(BlenderRnaRef))]
[JsonSerializable(typeof(BlenderCollectionItem))]
[JsonSerializable(typeof(BlenderCollectionItemMeta))]
[JsonSerializable(typeof(List<BlenderCollectionItem>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(BusinessError))]
[JsonSerializable(typeof(BusinessRequest))]
[JsonSerializable(typeof(BusinessResponse))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(double[]))]
public partial class ProtocolJsonContext : JsonSerializerContext
{
}
