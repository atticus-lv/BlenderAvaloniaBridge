using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace BlenderAvaloniaBridge.Protocol;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false)]
[JsonSerializable(typeof(ProtocolEnvelope))]
[JsonSerializable(typeof(BusinessError))]
[JsonSerializable(typeof(BusinessRequest))]
[JsonSerializable(typeof(BusinessResponse))]
[JsonSerializable(typeof(BusinessEvent))]
[JsonSerializable(typeof(BlenderValue))]
[JsonSerializable(typeof(BlenderNamedArg))]
[JsonSerializable(typeof(BlenderMethodCall))]
[JsonSerializable(typeof(BlenderOperatorCall))]
[JsonSerializable(typeof(BlenderContextOverride))]
[JsonSerializable(typeof(RnaPathRequest))]
[JsonSerializable(typeof(RnaSetRequest))]
[JsonSerializable(typeof(RnaCallRequest))]
[JsonSerializable(typeof(OpsPollRequest))]
[JsonSerializable(typeof(OpsCallRequest))]
[JsonSerializable(typeof(WatchSubscribeRequest))]
[JsonSerializable(typeof(WatchIdRequest))]
[JsonSerializable(typeof(PathRef))]
[JsonSerializable(typeof(RnaItemRef))]
[JsonSerializable(typeof(List<RnaItemRef>))]
[JsonSerializable(typeof(RnaDescribeResult))]
[JsonSerializable(typeof(RnaPropertyDescriptor))]
[JsonSerializable(typeof(OperatorPollResult))]
[JsonSerializable(typeof(OperatorCallResult))]
[JsonSerializable(typeof(WatchDirtyEvent))]
[JsonSerializable(typeof(WatchSnapshot))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool[]))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(long[]))]
[JsonSerializable(typeof(double[]))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(List<JsonElement>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal partial class ProtocolJsonContext : JsonSerializerContext
{
}
