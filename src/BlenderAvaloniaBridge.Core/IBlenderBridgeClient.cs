using System.Text.Json;
using BlenderAvaloniaBridge.Protocol;

namespace BlenderAvaloniaBridge;

public interface IBlenderBridgeClient
{
    Task RequestCollectionAsync(BlenderRnaRef owner, string dataPath, CancellationToken cancellationToken = default);

    Task RequestPropertyAsync(BlenderRnaRef target, string dataPath, CancellationToken cancellationToken = default);

    Task SetPropertyAsync(BlenderRnaRef target, string dataPath, JsonElement value, CancellationToken cancellationToken = default);

    Task CallOperatorAsync(
        string operatorName,
        string executionContext = "EXEC_DEFAULT",
        BlenderRnaRef? target = null,
        IReadOnlyDictionary<string, JsonElement>? properties = null,
        CancellationToken cancellationToken = default);
}
