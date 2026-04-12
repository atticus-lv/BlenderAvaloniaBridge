namespace BlenderAvaloniaBridge.Bridge;

public sealed class BridgeClient
{
    private readonly LengthPrefixedConnection _connection;
    private readonly HeadlessUiHost _uiHost;
    private int _sequence = 1;

    public BridgeClient(LengthPrefixedConnection connection, HeadlessUiHost uiHost)
    {
        _connection = connection;
        _uiHost = uiHost;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var packet = await _connection.ReadAsync(cancellationToken)
            ?? throw new InvalidOperationException("Bridge closed before init packet was received.");

        if (!string.Equals(packet.Header.Type, "init", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Expected init packet, got {packet.Header.Type}.");
        }

        if (packet.Header.Width.HasValue && packet.Header.Height.HasValue)
        {
            await _uiHost.ApplyAsync(packet.Header);
        }
        else
        {
            await _uiHost.InitializeAsync();
        }

        await _connection.WriteAsync(_uiHost.CreateInitAck(_sequence++), cancellationToken);
        await _connection.WriteAsync(await _uiHost.CaptureFrameAsync(_sequence++), cancellationToken);

        while (true)
        {
            var next = await _connection.ReadAsync(cancellationToken);
            if (next is null)
            {
                break;
            }

            await _uiHost.ApplyAsync(next.Header);
            if (!string.Equals(next.Header.Type, "focus", StringComparison.OrdinalIgnoreCase) || next.Header.Focus == true)
            {
                await _connection.WriteAsync(await _uiHost.CaptureFrameAsync(_sequence++), cancellationToken);
            }
        }
    }
}
