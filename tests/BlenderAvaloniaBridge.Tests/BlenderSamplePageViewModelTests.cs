using System.Collections.Generic;
using System.Text.Json;
using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Sample.ViewModels;
using BlenderAvaloniaBridge.Sample.ViewModels.Pages;
using Xunit;

namespace BlenderAvaloniaBridge.Tests;

public sealed class BlenderSamplePageViewModelTests
{
    [Fact]
    public async Task MainViewModel_NavigationUpdatesSelectedPageState()
    {
        var viewModel = new MainViewModel();

        Assert.True(viewModel.IsButtonsPageSelected);

        viewModel.ShowBlenderObjectsPageCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsBlenderObjectsPageSelected);
        Assert.Equal("Blender Objects", viewModel.CurrentPageTitle);

        viewModel.ShowLiveTransformPageCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsLiveTransformPageSelected);
        Assert.Equal("Live Transform", viewModel.CurrentPageTitle);

        viewModel.ShowMaterialsPageCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsMaterialsPageSelected);
        Assert.Equal("Materials", viewModel.CurrentPageTitle);

        viewModel.ShowCollectionsPageCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsCollectionsPageSelected);
        Assert.Equal("Collections", viewModel.CurrentPageTitle);

        viewModel.ShowOperatorsPageCommand.Execute(null);
        await WaitForAsync(() => viewModel.IsOperatorsPageSelected);
        Assert.Equal("Operator Playground", viewModel.CurrentPageTitle);
    }

    [Fact]
    public async Task BlenderObjectsPage_Activate_LoadsObjectsAndProperties()
    {
        var viewModel = new BlenderObjectsPageViewModel();
        var api = new TestBlenderDataApi
        {
            ListAsyncImpl = (path, _) => Task.FromResult<IReadOnlyList<RnaItemRef>>(
                [
                    CreateObject("Cube", "bpy.data.objects[\"Cube\"]", 7, isActive: true),
                ]),
            GetAsyncImpl = (path, _) => Task.FromResult<object?>(
                path.EndsWith(".name", StringComparison.Ordinal) ? "Cube" : new[] { 1.0, 2.0, 3.0 }),
        };

        viewModel.AttachBlenderDataApi(api);
        await viewModel.ActivateAsync();

        await WaitForAsync(() => viewModel.Objects.Count == 1 && viewModel.ObjectName == "Cube");

        Assert.Equal("Cube", viewModel.SelectedObject?.Label);
        Assert.Equal("1", viewModel.LocationX);
        Assert.Equal("2", viewModel.LocationY);
        Assert.Equal("3", viewModel.LocationZ);
    }

    [Fact]
    public async Task BlenderObjectsPage_CommitNameFailure_StoresErrorStatus()
    {
        var viewModel = new BlenderObjectsPageViewModel();
        var api = new TestBlenderDataApi
        {
            ListAsyncImpl = (_, _) => Task.FromResult<IReadOnlyList<RnaItemRef>>([CreateObject("Cube", "bpy.data.objects[\"Cube\"]", 7, isActive: true)]),
            GetAsyncImpl = (path, _) => Task.FromResult<object?>(
                path.EndsWith(".name", StringComparison.Ordinal) ? "Cube" : new[] { 0.0, 0.0, 0.0 }),
            SetAsyncImpl = (_, _, _) => throw new InvalidOperationException("name set failed"),
        };

        viewModel.AttachBlenderDataApi(api);
        await viewModel.ActivateAsync();
        await WaitForAsync(() => viewModel.SelectedObject is not null);

        viewModel.ObjectName = "Cube.001";
        await viewModel.CommitNameAsync();

        Assert.Equal("name set failed", viewModel.StatusText);
    }

    [Fact]
    public async Task LiveTransformPage_OnlySubscribesWhenCheckboxIsEnabled()
    {
        var viewModel = new LiveTransformPageViewModel();
        var api = new TestBlenderDataApi
        {
            GetAsyncImpl = (path, _) => Task.FromResult<object?>(
                path == "bpy.context.object" ? CreateObject("Cube", "bpy.data.objects[\"Cube\"]", 3, isActive: true) :
                path.EndsWith(".scale", StringComparison.Ordinal) ? new[] { 1.0, 1.0, 1.0 } :
                path.EndsWith(".rotation_euler", StringComparison.Ordinal) ? new[] { 0.1, 0.2, 0.3 } :
                new[] { 4.0, 5.0, 6.0 }),
        };

        viewModel.AttachBlenderDataApi(api);
        await viewModel.ActivateAsync();
        await WaitForAsync(() => viewModel.CurrentObject is not null);

        Assert.Equal(0, api.WatchSubscribeCount);

        viewModel.IsLiveWatchEnabled = true;
        await WaitForAsync(() => api.WatchSubscribeCount == 1);

        var getCountBeforeDirty = api.GetPaths.Count;
        await api.TriggerWatchDirtyAsync("live-transform-3");
        await WaitForAsync(() => api.GetPaths.Count > getCountBeforeDirty);

        viewModel.IsLiveWatchEnabled = false;
        await WaitForAsync(() => api.WatchDisposeCount == 1);
    }

    [Fact]
    public async Task MaterialsPage_CreateMaterialAndDirtyEvent_ReloadsList()
    {
        var viewModel = new MaterialsPageViewModel();
        var api = new TestBlenderDataApi
        {
            ListAsyncImpl = (path, _) => Task.FromResult<IReadOnlyList<RnaItemRef>>(
                [
                    new RnaItemRef
                    {
                        Path = "bpy.data.materials[\"Mat_A\"]",
                        Name = "Mat_A",
                        Label = "Mat_A",
                        RnaType = "Material",
                    },
                ]),
            GetAsyncImpl = (path, _) => Task.FromResult<object?>(
                path.EndsWith(".use_nodes", StringComparison.Ordinal) ? true : "Mat_A"),
            CallAsyncImpl = (path, method, kwargs, _) =>
            {
                Assert.Equal("bpy.data.materials", path);
                Assert.Equal("new", method);
                Assert.Single(kwargs);
                Assert.Equal("name", kwargs[0].Name);
                return Task.FromResult<object?>(
                    new RnaItemRef
                    {
                        Path = "bpy.data.materials[\"Mat_New\"]",
                        Name = "Mat_New",
                        Label = "Mat_New",
                        RnaType = "Material",
                    });
            },
        };

        viewModel.AttachBlenderDataApi(api);
        await viewModel.ActivateAsync();
        await WaitForAsync(() => viewModel.Materials.Count == 1);

        viewModel.NewMaterialName = "Mat_New";
        await viewModel.CreateMaterialAsync();
        Assert.Equal(1, api.WatchSubscribeCount);

        var listCallCount = api.ListedPaths.Count;
        await api.TriggerWatchDirtyAsync("materials-page");
        await WaitForAsync(() => api.ListedPaths.Count > listCallCount);
    }

    [Fact]
    public async Task CollectionsPage_SelectCollection_LoadsChildrenAndObjects()
    {
        var viewModel = new CollectionsPageViewModel();
        var api = new TestBlenderDataApi
        {
            GetAsyncImpl = (path, _) => path switch
            {
                "bpy.context.scene.collection" => Task.FromResult<object?>(
                    new RnaItemRef
                    {
                        Path = "bpy.context.scene.collection",
                        Name = "Root",
                        Label = "Root",
                        RnaType = "Collection",
                    }),
                _ => Task.FromResult<object?>(null),
            },
            ListAsyncImpl = (path, _) => Task.FromResult<IReadOnlyList<RnaItemRef>>(path switch
            {
                "bpy.context.scene.collection.children" =>
                [
                    new RnaItemRef
                    {
                        Path = "bpy.data.collections[\"Zoo\"]",
                        Name = "Zoo",
                        Label = "Zoo",
                        RnaType = "Collection",
                    },
                    new RnaItemRef
                    {
                        Path = "bpy.data.collections[\"Child\"]",
                        Name = "Child",
                        Label = "Child",
                        RnaType = "Collection",
                    },
                ],
                "bpy.context.scene.collection.objects" =>
                [
                    CreateObject("Sphere", "bpy.data.objects[\"Sphere\"]", 11),
                    CreateObject("Cube", "bpy.data.objects[\"Cube\"]", 9),
                ],
                "bpy.data.collections[\"Child\"].children" => [],
                "bpy.data.collections[\"Child\"].objects" =>
                [
                    CreateObject("Torus", "bpy.data.objects[\"Torus\"]", 10),
                ],
                "bpy.data.collections[\"Zoo\"].children" => [],
                "bpy.data.collections[\"Zoo\"].objects" => [],
                _ => Array.Empty<RnaItemRef>(),
            }),
        };

        viewModel.AttachBlenderDataApi(api);
        await viewModel.ActivateAsync();
        await WaitForAsync(() =>
            viewModel.CollectionTreeRoots.Count == 1 &&
            viewModel.SelectedCollectionNode is not null &&
            viewModel.SelectedCollectionNode.Children.Count == 4 &&
            viewModel.CollectionObjects.Count == 2);

        Assert.NotNull(viewModel.SelectedCollectionNode);
        Assert.Equal("Root", viewModel.SelectedCollectionNode!.Item.Name);
        Assert.True(viewModel.SelectedCollectionNode.Children[0].IsCollection);
        Assert.Equal("Child", viewModel.SelectedCollectionNode.Children[0].Item.Name);
        Assert.True(viewModel.SelectedCollectionNode.Children[1].IsCollection);
        Assert.Equal("Zoo", viewModel.SelectedCollectionNode.Children[1].Item.Name);
        Assert.True(viewModel.SelectedCollectionNode.Children[2].IsObject);
        Assert.Equal("Cube", viewModel.SelectedCollectionNode.Children[2].Item.Name);
        Assert.True(viewModel.SelectedCollectionNode.Children[3].IsObject);
        Assert.Equal("Sphere", viewModel.SelectedCollectionNode.Children[3].Item.Name);
        Assert.Equal("Cube", viewModel.CollectionObjects[0].Name);
        Assert.Equal("Sphere", viewModel.CollectionObjects[1].Name);

        viewModel.SelectedCollectionNode = viewModel.SelectedCollectionNode.Children[0];
        await WaitForAsync(() => viewModel.CollectionObjects.Count == 1 && viewModel.CollectionObjects[0].Name == "Torus");
        Assert.Equal("Torus", viewModel.CollectionObjects[0].Name);
    }

    [Fact]
    public async Task OperatorsPage_PollsAndUsesContextOverrideForSelectionOperators()
    {
        var viewModel = new OperatorsPageViewModel();
        var api = new TestBlenderDataApi
        {
            GetAsyncImpl = (path, _) => Task.FromResult<object?>(
                path == "bpy.context.object" ? CreateObject("Cube", "bpy.data.objects[\"Cube\"]", 17, isActive: true) : null),
            PollOperatorImpl = (operatorName, _, contextOverride, _) => Task.FromResult(
                new OperatorPollResult
                {
                    OperatorName = operatorName,
                    CanExecute = true,
                    FailureReason = null,
                }),
            CallOperatorImpl = (operatorName, call, _) =>
            {
                if (operatorName == "object.duplicate_move")
                {
                    Assert.NotNull(call.ContextOverride);
                    Assert.Equal("bpy.data.objects[\"Cube\"]", call.ContextOverride!.ActiveObject);
                }

                return Task.FromResult(
                    new OperatorCallResult
                    {
                        OperatorName = operatorName,
                        Result = ["FINISHED"],
                    });
            },
        };

        viewModel.AttachBlenderDataApi(api);
        await viewModel.ActivateAsync();
        await WaitForAsync(() => viewModel.CurrentObject is not null && viewModel.CanDuplicateOperator);

        await viewModel.DuplicateAsync();

        Assert.Equal("Ready", viewModel.DuplicatePollText);
        Assert.Contains(api.OperatorPollCalls, call => call.operatorName == "object.duplicate_move");
        Assert.Contains(api.OperatorCallRecords, call => call.operatorName == "object.duplicate_move");
    }

    private static RnaItemRef CreateObject(string name, string path, long sessionUid, bool isActive = false)
    {
        return new RnaItemRef
        {
            Path = path,
            Name = name,
            Label = name,
            RnaType = "Object",
            IdType = "OBJECT",
            SessionUid = sessionUid,
            Metadata = JsonDocument.Parse($"{{\"objectType\":\"MESH\",\"isActive\":{(isActive ? "true" : "false")}}}").RootElement.Clone(),
        };
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

    private sealed class TestBlenderDataApi : IBlenderDataApi
    {
        public Func<string, CancellationToken, Task<IReadOnlyList<RnaItemRef>>>? ListAsyncImpl { get; init; }

        public Func<string, CancellationToken, Task<object?>>? GetAsyncImpl { get; init; }

        public Func<string, object?, CancellationToken, Task>? SetAsyncImpl { get; init; }

        public Func<string, string, IReadOnlyList<BlenderNamedArg>, CancellationToken, Task<object?>>? CallAsyncImpl { get; init; }

        public Func<string, string, BlenderContextOverride?, CancellationToken, Task<OperatorPollResult>>? PollOperatorImpl { get; init; }

        public Func<string, BlenderOperatorCall, CancellationToken, Task<OperatorCallResult>>? CallOperatorImpl { get; init; }

        public List<string> ListedPaths { get; } = new();

        public List<string> GetPaths { get; } = new();

        public List<(string path, object? value)> SetCalls { get; } = new();

        public List<(string operatorName, string operatorContext, BlenderContextOverride? contextOverride)> OperatorPollCalls { get; } = new();

        public List<(string operatorName, BlenderOperatorCall call)> OperatorCallRecords { get; } = new();

        public int WatchSubscribeCount { get; private set; }

        public int WatchDisposeCount { get; private set; }

        private Func<WatchDirtyEvent, Task>? _watchCallback;

        public Task<IReadOnlyList<RnaItemRef>> ListAsync(string path, CancellationToken cancellationToken = default)
        {
            ListedPaths.Add(path);
            return ListAsyncImpl?.Invoke(path, cancellationToken) ?? Task.FromResult<IReadOnlyList<RnaItemRef>>(Array.Empty<RnaItemRef>());
        }

        public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default)
        {
            GetPaths.Add(path);
            return (T)(await (GetAsyncImpl?.Invoke(path, cancellationToken) ?? Task.FromResult<object?>(default(T))) ?? throw new InvalidOperationException("No value."));
        }

        public Task SetAsync<T>(string path, T value, CancellationToken cancellationToken = default)
        {
            SetCalls.Add((path, value));
            return SetAsyncImpl?.Invoke(path, value, cancellationToken) ?? Task.CompletedTask;
        }

        public Task<RnaDescribeResult> DescribeAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async Task<T> CallAsync<T>(string path, string method, params BlenderNamedArg[] kwargs)
        {
            return (T)(await (CallAsyncImpl?.Invoke(path, method, kwargs, CancellationToken.None) ?? Task.FromResult<object?>(default(T))) ?? throw new InvalidOperationException("No call result."));
        }

        public Task<T> CallAsync<T>(string path, string method, BlenderMethodCall call, CancellationToken cancellationToken = default)
        {
            var kwargs = call.Kwargs ?? Array.Empty<BlenderNamedArg>();
            return CallAsync<T>(path, method, kwargs.ToArray());
        }

        public Task<OperatorPollResult> PollOperatorAsync(string operatorName, string operatorContext = "EXEC_DEFAULT", BlenderContextOverride? contextOverride = null, CancellationToken cancellationToken = default)
        {
            OperatorPollCalls.Add((operatorName, operatorContext, contextOverride));
            return PollOperatorImpl?.Invoke(operatorName, operatorContext, contextOverride, cancellationToken)
                   ?? Task.FromResult(
                       new OperatorPollResult
                       {
                           OperatorName = operatorName,
                           CanExecute = true,
                       });
        }

        public Task<OperatorCallResult> CallOperatorAsync(string operatorName, params BlenderNamedArg[] properties)
        {
            return CallOperatorAsync(
                operatorName,
                new BlenderOperatorCall
                {
                    Properties = properties,
                });
        }

        public Task<OperatorCallResult> CallOperatorAsync(string operatorName, BlenderOperatorCall call, CancellationToken cancellationToken = default)
        {
            OperatorCallRecords.Add((operatorName, call));
            return CallOperatorImpl?.Invoke(operatorName, call, cancellationToken)
                   ?? Task.FromResult(
                       new OperatorCallResult
                       {
                           OperatorName = operatorName,
                           Result = ["FINISHED"],
                       });
        }

        public Task<IAsyncDisposable> WatchAsync(string watchId, WatchSource source, string path, Func<WatchDirtyEvent, Task> onDirty, CancellationToken cancellationToken = default)
        {
            WatchSubscribeCount++;
            _watchCallback = onDirty;
            return Task.FromResult<IAsyncDisposable>(new TrackingAsyncDisposable(this));
        }

        public Task<WatchSnapshot> ReadWatchAsync(string watchId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new WatchSnapshot
            {
                WatchId = watchId,
                Revision = 1,
                Source = WatchSource.Depsgraph,
            });
        }

        public Task TriggerWatchDirtyAsync(string watchId)
        {
            return _watchCallback?.Invoke(
                new WatchDirtyEvent
                {
                    WatchId = watchId,
                    Revision = 1,
                    Source = WatchSource.Depsgraph,
                }) ?? Task.CompletedTask;
        }

        private sealed class TrackingAsyncDisposable : IAsyncDisposable
        {
            private readonly TestBlenderDataApi _owner;

            public TrackingAsyncDisposable(TestBlenderDataApi owner)
            {
                _owner = owner;
            }

            public ValueTask DisposeAsync()
            {
                _owner.WatchDisposeCount++;
                return ValueTask.CompletedTask;
            }
        }
    }
}
