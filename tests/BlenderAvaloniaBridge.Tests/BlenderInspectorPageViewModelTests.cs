using System.Collections.Generic;
using System.Text.Json;
using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Sample.ViewModels;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class BlenderInspectorPageViewModelTests
{
    [Fact]
    public async Task AttachBlenderDataApi_WhenInitialRefreshFails_StoresErrorStatus()
    {
        var viewModel = new BlenderInspectorPageViewModel();
        var api = new DelegateBlenderDataApi(
            listAsync: (_, _) => throw new InvalidOperationException("refresh failed"));

        viewModel.AttachBlenderDataApi(api);

        await WaitForAsync(() => viewModel.StatusText == "refresh failed");

        Assert.Equal("refresh failed", viewModel.StatusText);
    }

    [Fact]
    public async Task CommitNameAsync_WhenSetFails_DoesNotPropagateAndStoresErrorStatus()
    {
        var viewModel = new BlenderInspectorPageViewModel();
        var api = new DelegateBlenderDataApi(
            listAsync: (_, _) => Task.FromResult<IReadOnlyList<RnaItemRef>>(Array.Empty<RnaItemRef>()),
            setAsync: (_, _, _) => throw new InvalidOperationException("name set failed"));

        viewModel.AttachBlenderDataApi(api);
        viewModel.SelectedObject = new BlenderObjectListItem(
            new RnaItemRef
            {
                Path = "bpy.data.objects[\"Cube\"]",
                Name = "Cube",
                Label = "Cube",
                SessionUid = 1,
                RnaType = "Object",
            },
            "Cube",
            "MESH",
            true);
        viewModel.ObjectName = "Cube.001";

        var exception = await Record.ExceptionAsync(() => viewModel.CommitNameAsync());

        Assert.Null(exception);
        Assert.Equal("name set failed", viewModel.StatusText);
    }

    [Fact]
    public async Task AttachBlenderDataApi_WhenInitialRefreshSucceeds_PopulatesObjects()
    {
        var viewModel = new BlenderInspectorPageViewModel();
        var api = new DelegateBlenderDataApi(
            listAsync: (_, _) => Task.FromResult<IReadOnlyList<RnaItemRef>>(
                new[]
                {
                    new RnaItemRef
                    {
                        Path = "bpy.data.objects[\"Cube\"]",
                        Name = "Cube",
                        Label = "Cube",
                        RnaType = "Object",
                        IdType = "OBJECT",
                        SessionUid = 7,
                        Metadata = JsonDocument.Parse("{\"objectType\":\"MESH\",\"isActive\":true}").RootElement.Clone()
                    }
                }),
            getAsync: (path, _) => Task.FromResult<object?>(
                path.EndsWith(".name", StringComparison.Ordinal) ? "Cube" : new[] { 0.0, 0.0, 0.0 }));

        viewModel.AttachBlenderDataApi(api);

        await WaitForAsync(() => viewModel.Objects.Count == 1);

        Assert.Equal("Cube", viewModel.Objects[0].Label);
        Assert.Equal("MESH", viewModel.Objects[0].ObjectType);
        Assert.Equal("Object", viewModel.SelectedObject?.RnaRef.RnaType);
    }

    [Fact]
    public async Task CommitNameAsync_WhenPropertySetSucceeds_RefreshesSelectedObject()
    {
        var viewModel = new BlenderInspectorPageViewModel();
        var listedItems = new[]
        {
            new RnaItemRef
            {
                Path = "bpy.data.objects[\"Cube.001\"]",
                Name = "Cube.001",
                Label = "Cube.001",
                RnaType = "Object",
                IdType = "OBJECT",
                SessionUid = 9,
                Metadata = JsonDocument.Parse("{\"objectType\":\"MESH\",\"isActive\":true}").RootElement.Clone()
            }
        };
        var api = new DelegateBlenderDataApi(
            listAsync: (_, _) => Task.FromResult<IReadOnlyList<RnaItemRef>>(listedItems),
            getAsync: (path, _) => Task.FromResult<object?>(
                path.EndsWith(".name", StringComparison.Ordinal) ? "Cube.001" : new[] { 0.0, 0.0, 0.0 }),
            setAsync: (_, _, _) => Task.CompletedTask);

        viewModel.AttachBlenderDataApi(api);
        viewModel.SelectedObject = new BlenderObjectListItem(
            new RnaItemRef
            {
                Path = "bpy.data.objects[\"Cube\"]",
                Name = "Cube",
                Label = "Cube",
                SessionUid = 9,
                RnaType = "Object",
            },
            "Cube",
            "MESH",
            true);
        viewModel.ObjectName = "Cube.001";

        await viewModel.CommitNameAsync();

        Assert.Equal("Cube.001", viewModel.ObjectName);
        Assert.Equal("Cube.001", viewModel.SelectedObject?.Label);
        Assert.Equal("Cube.001", viewModel.SelectedObject?.RnaRef.Name);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeoutAt)
            {
                throw new TimeoutException("Timed out waiting for condition.");
            }

            await Task.Delay(25);
        }
    }

    private sealed class DelegateBlenderDataApi : IBlenderDataApi
    {
        private readonly Func<string, CancellationToken, Task<IReadOnlyList<RnaItemRef>>> _listAsync;
        private readonly Func<string, CancellationToken, Task<object?>> _getAsync;
        private readonly Func<string, object?, CancellationToken, Task> _setAsync;

        public DelegateBlenderDataApi(
            Func<string, CancellationToken, Task<IReadOnlyList<RnaItemRef>>>? listAsync = null,
            Func<string, CancellationToken, Task<object?>>? getAsync = null,
            Func<string, object?, CancellationToken, Task>? setAsync = null)
        {
            _listAsync = listAsync ?? ((_, _) => Task.FromResult<IReadOnlyList<RnaItemRef>>(Array.Empty<RnaItemRef>()));
            _getAsync = getAsync ?? ((_, _) => Task.FromResult<object?>(null));
            _setAsync = setAsync ?? ((_, _, _) => Task.CompletedTask);
        }

        public Task<IReadOnlyList<RnaItemRef>> ListAsync(string path, CancellationToken cancellationToken = default) => _listAsync(path, cancellationToken);

        public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default)
        {
            return (T)(await _getAsync(path, cancellationToken) ?? throw new InvalidOperationException("No value."));
        }

        public Task SetAsync<T>(string path, T value, CancellationToken cancellationToken = default) => _setAsync(path, value, cancellationToken);

        public Task<RnaDescribeResult> DescribeAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<T> CallAsync<T>(string path, string method, params BlenderNamedArg[] kwargs) => throw new NotSupportedException();

        public Task<T> CallAsync<T>(string path, string method, BlenderMethodCall call, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OperatorPollResult> PollOperatorAsync(string operatorName, string operatorContext = "EXEC_DEFAULT", BlenderContextOverride? contextOverride = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<OperatorCallResult> CallOperatorAsync(string operatorName, params BlenderNamedArg[] properties)
        {
            return Task.FromResult(new OperatorCallResult
            {
                OperatorName = operatorName,
                Result = new List<string> { "FINISHED" }
            });
        }

        public Task<OperatorCallResult> CallOperatorAsync(string operatorName, BlenderOperatorCall call, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OperatorCallResult
            {
                OperatorName = operatorName,
                Result = new List<string> { "FINISHED" }
            });
        }

        public Task<IAsyncDisposable> WatchAsync(string watchId, WatchSource source, string path, Func<WatchDirtyEvent, Task> onDirty, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WatchSnapshot> ReadWatchAsync(string watchId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
