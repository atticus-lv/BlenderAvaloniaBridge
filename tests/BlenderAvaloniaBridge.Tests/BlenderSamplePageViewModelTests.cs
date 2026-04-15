using System.Collections.Generic;
using System.Text.Json;
using BlenderAvaloniaBridge;
using BlenderAvaloniaBridge.Sample.Helpers;
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
        var blenderApi = new TestBlenderApi
        {
            ListAsyncImpl = (path, _) => Task.FromResult<IReadOnlyList<RnaItemRef>>(
                [
                    CreateObject("Cube", "bpy.data.objects[\"Cube\"]", 7, isActive: true),
                ]),
            GetAsyncImpl = (path, _) => Task.FromResult<object?>(path switch
            {
                BlenderSampleDataHelpers.ActiveObjectPath => CreateObject("Cube", "bpy.data.objects[\"Cube\"]", 7, isActive: true),
                var value when value.EndsWith(".name", StringComparison.Ordinal) => "Cube",
                var value when value.EndsWith(".type", StringComparison.Ordinal) => "MESH",
                _ => new[] { 1.0, 2.0, 3.0 },
            }),
        };

        viewModel.AttachBlenderApi(blenderApi);
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
        var blenderApi = new TestBlenderApi
        {
            ListAsyncImpl = (_, _) => Task.FromResult<IReadOnlyList<RnaItemRef>>([CreateObject("Cube", "bpy.data.objects[\"Cube\"]", 7, isActive: true)]),
            GetAsyncImpl = (path, _) => Task.FromResult<object?>(path switch
            {
                BlenderSampleDataHelpers.ActiveObjectPath => CreateObject("Cube", "bpy.data.objects[\"Cube\"]", 7, isActive: true),
                var value when value.EndsWith(".name", StringComparison.Ordinal) => "Cube",
                var value when value.EndsWith(".type", StringComparison.Ordinal) => "MESH",
                _ => new[] { 0.0, 0.0, 0.0 },
            }),
            SetAsyncImpl = (_, _, _) => throw new InvalidOperationException("name set failed"),
        };

        viewModel.AttachBlenderApi(blenderApi);
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
        var blenderApi = new TestBlenderApi
        {
            GetAsyncImpl = (path, _) => Task.FromResult<object?>(
                path == "bpy.context.object" ? CreateObject("Cube", "bpy.data.objects[\"Cube\"]", 3, isActive: true) :
                path.EndsWith(".scale", StringComparison.Ordinal) ? new[] { 1.0, 1.0, 1.0 } :
                path.EndsWith(".rotation_euler", StringComparison.Ordinal) ? new[] { 0.1, 0.2, 0.3 } :
                new[] { 4.0, 5.0, 6.0 }),
        };

        viewModel.AttachBlenderApi(blenderApi);
        await viewModel.ActivateAsync();
        await WaitForAsync(() => viewModel.CurrentObject is not null);

        Assert.Equal(0, blenderApi.WatchSubscribeCount);

        viewModel.IsLiveWatchEnabled = true;
        await WaitForAsync(() => blenderApi.WatchSubscribeCount == 1);

        var getCountBeforeDirty = blenderApi.GetPaths.Count;
        await blenderApi.TriggerWatchDirtyAsync("live-transform-3");
        await WaitForAsync(() => blenderApi.GetPaths.Count > getCountBeforeDirty);

        viewModel.IsLiveWatchEnabled = false;
        await WaitForAsync(() => blenderApi.WatchDisposeCount == 1);
    }

    [Fact]
    public async Task MaterialsPage_CreateMaterialAndDirtyEvent_ReloadsList()
    {
        var viewModel = new MaterialsPageViewModel();
        var blenderApi = new TestBlenderApi
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
                    path switch
                {
                    var value when value.EndsWith(".name", StringComparison.Ordinal) => "Mat_A",
                    var value when value.EndsWith(".preview.icon_size", StringComparison.Ordinal) => new[] { 1, 1 },
                    var value when value.EndsWith(".preview.icon_pixels", StringComparison.Ordinal) => new[] { 40, 120, 220, 255 },
                    var value when value.EndsWith(".preview.image_size", StringComparison.Ordinal) => new[] { 2, 1 },
                    var value when value.EndsWith(".preview.image_pixels", StringComparison.Ordinal) => new[] { 40, 120, 220, 255, 220, 240, 255, 255 },
                    _ => null,
                }),
            CallAsyncImpl = (path, method, kwargs, _) =>
            {
                if (path == "bpy.data.materials" && method == "new")
                {
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
                }

                if (path == "bpy.data.materials[\"Mat_A\"]" && method == "preview_ensure")
                {
                    return Task.FromResult<object?>(
                        new RnaItemRef
                        {
                            Path = $"{path}.preview",
                            Name = "preview",
                            Label = "preview",
                            RnaType = "ImagePreview",
                        });
                }

                throw new InvalidOperationException($"Unexpected RNA call: {path}.{method}");
            },
        };

        viewModel.AttachBlenderApi(blenderApi);
        await viewModel.ActivateAsync();
        await WaitForAsync(() => viewModel.Materials.Count == 1);

        viewModel.NewMaterialName = "Mat_New";
        await viewModel.CreateMaterialAsync();
        Assert.Equal(1, blenderApi.WatchSubscribeCount);

        var listCallCount = blenderApi.ListedPaths.Count;
        await blenderApi.TriggerWatchDirtyAsync("materials-page");
        await WaitForAsync(() => blenderApi.ListedPaths.Count > listCallCount);
    }

    [Fact]
    public async Task MaterialsPage_Activate_LoadsMaterialLibraryCardsAndPreviewImages()
    {
        var viewModel = new MaterialsPageViewModel();
        var blenderApi = new TestBlenderApi
        {
            ListAsyncImpl = (path, _) => Task.FromResult<IReadOnlyList<RnaItemRef>>(
                [
                    new RnaItemRef
                    {
                        Path = "bpy.data.materials[\"Mat_A\"]",
                        Name = "Mat_A",
                        Label = "Mat_A",
                        RnaType = "Material",
                        IdType = "MATERIAL",
                        SessionUid = 41,
                    },
                ]),
            GetAsyncImpl = (path, _) => Task.FromResult<object?>(
                path switch
                {
                    var value when value.EndsWith(".name", StringComparison.Ordinal) => "Mat_A",
                    var value when value.EndsWith(".preview.icon_size", StringComparison.Ordinal) => new[] { 2, 1 },
                    var value when value.EndsWith(".preview.image_size", StringComparison.Ordinal) => new[] { 1, 2 },
                    _ => null,
                }),
            ReadArrayAsyncImpl = (path, _) => Task.FromResult(
                path switch
                {
                    var value when value.EndsWith(".preview.icon_pixels", StringComparison.Ordinal) => new BlenderArrayReadResult
                    {
                        Path = path,
                        RnaType = "ImagePreview",
                        ElementType = "uint8",
                        Count = 8,
                        Shape = [1, 2, 4],
                        RawBytes = [255, 32, 64, 255, 32, 180, 255, 255],
                    },
                    var value when value.EndsWith(".preview.image_pixels", StringComparison.Ordinal) => new BlenderArrayReadResult
                    {
                        Path = path,
                        RnaType = "ImagePreview",
                        ElementType = "uint8",
                        Count = 8,
                        Shape = [2, 1, 4],
                        RawBytes = [255, 220, 120, 255, 40, 60, 100, 255],
                    },
                    _ => throw new InvalidOperationException($"Unexpected array path: {path}"),
                }),
        };

        viewModel.AttachBlenderApi(blenderApi);
        await viewModel.ActivateAsync();
        await WaitForAsync(() =>
            viewModel.Materials.Count == 1 &&
            viewModel.SelectedMaterial is not null);

        Assert.Equal("Mat_A", viewModel.Materials[0].DisplayName);
        Assert.Equal("bpy.data.materials[\"Mat_A\"]", viewModel.SelectedMaterialPath);
        Assert.Equal("Loaded material Mat_A.", viewModel.StatusText);
        Assert.DoesNotContain(
            blenderApi.GetPaths,
            path => path.EndsWith(".use_nodes", StringComparison.Ordinal));
        Assert.Contains(
            blenderApi.ReadArrayPaths,
            path => path.EndsWith(".preview.icon_pixels", StringComparison.Ordinal));
        Assert.Contains(
            blenderApi.ReadArrayPaths,
            path => path.EndsWith(".preview.image_pixels", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MaterialsPage_MissingPreview_EnsuresPreviewBeforeRetrying()
    {
        var previewReady = false;
        var previewEnsureCalls = 0;
        var viewModel = new MaterialsPageViewModel();
        var blenderApi = new TestBlenderApi
        {
            ListAsyncImpl = (_, _) => Task.FromResult<IReadOnlyList<RnaItemRef>>(
                [
                    new RnaItemRef
                    {
                        Path = "bpy.data.materials[\"Mat_A\"]",
                        Name = "Mat_A",
                        Label = "Mat_A",
                        RnaType = "Material",
                        IdType = "MATERIAL",
                    },
                ]),
            GetAsyncImpl = (path, _) => Task.FromResult<object?>(
                path switch
                {
                    var value when value.EndsWith(".name", StringComparison.Ordinal) => "Mat_A",
                    var value when value.EndsWith(".preview.icon_size", StringComparison.Ordinal) => previewReady ? new[] { 1, 1 } : null,
                    var value when value.EndsWith(".preview.image_size", StringComparison.Ordinal) => previewReady ? new[] { 1, 1 } : null,
                    _ => null,
                }),
            ReadArrayAsyncImpl = (path, _) =>
            {
                if (previewReady)
                {
                    return Task.FromResult(
                        new BlenderArrayReadResult
                        {
                            Path = path,
                            RnaType = "ImagePreview",
                            ElementType = "uint8",
                            Count = 4,
                            Shape = [1, 1, 4],
                            RawBytes = [90, 160, 255, 255],
                        });
                }

                throw new InvalidOperationException("No value.");
            },
            CallAsyncImpl = (path, method, _, _) =>
            {
                if (path == "bpy.data.materials[\"Mat_A\"]" && method == "preview_ensure")
                {
                    previewEnsureCalls++;
                    previewReady = true;
                }

                return Task.FromResult<object?>(
                    new RnaItemRef
                    {
                        Path = $"{path}.preview",
                        Name = "preview",
                        Label = "preview",
                        RnaType = "ImagePreview",
                    });
            },
        };

        viewModel.AttachBlenderApi(blenderApi);
        await viewModel.ActivateAsync();
        await WaitForAsync(() => viewModel.Materials.Count == 1 && viewModel.SelectedMaterial is not null);

        Assert.True(previewEnsureCalls > 0, "Expected preview_ensure to be called when preview data is missing.");
        Assert.Equal("Loaded material Mat_A.", viewModel.StatusText);
    }

    [Fact]
    public async Task CollectionsPage_SelectCollection_LoadsChildrenAndObjects()
    {
        var viewModel = new CollectionsPageViewModel();
        var blenderApi = new TestBlenderApi
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

        viewModel.AttachBlenderApi(blenderApi);
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
        var blenderApi = new TestBlenderApi
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

        viewModel.AttachBlenderApi(blenderApi);
        await viewModel.ActivateAsync();
        await WaitForAsync(() => viewModel.CurrentObject is not null && viewModel.CanDuplicateOperator);

        await viewModel.DuplicateAsync();

        Assert.Equal("Ready", viewModel.DuplicatePollText);
        Assert.Contains(blenderApi.OperatorPollCalls, call => call.operatorName == "object.duplicate_move");
        Assert.Contains(blenderApi.OperatorCallRecords, call => call.operatorName == "object.duplicate_move");
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

    private sealed class TestBlenderApi : BlenderApi
    {
        public TestBlenderApi()
            : base(new ThrowingBusinessEndpoint())
        {
            base.Rna = new TestBlenderRnaApi(this);
            base.Ops = new TestBlenderOpsApi(this);
            base.Observe = new TestBlenderObserveApi(this);
        }

        public new TestBlenderRnaApi Rna => (TestBlenderRnaApi)base.Rna;

        public new TestBlenderOpsApi Ops => (TestBlenderOpsApi)base.Ops;

        public new TestBlenderObserveApi Observe => (TestBlenderObserveApi)base.Observe;

        public Func<string, CancellationToken, Task<IReadOnlyList<RnaItemRef>>>? ListAsyncImpl { get; init; }

        public Func<string, CancellationToken, Task<object?>>? GetAsyncImpl { get; init; }

        public Func<string, object?, CancellationToken, Task>? SetAsyncImpl { get; init; }

        public Func<string, string, IReadOnlyList<BlenderNamedArg>, CancellationToken, Task<object?>>? CallAsyncImpl { get; init; }

        public Func<string, CancellationToken, Task<BlenderArrayReadResult>>? ReadArrayAsyncImpl { get; init; }

        public Func<string, string, BlenderContextOverride?, CancellationToken, Task<OperatorPollResult>>? PollOperatorImpl { get; init; }

        public Func<string, BlenderOperatorCall, CancellationToken, Task<OperatorCallResult>>? CallOperatorImpl { get; init; }

        public List<string> ListedPaths { get; } = new();

        public List<string> GetPaths { get; } = new();

        public List<string> ReadArrayPaths { get; } = new();

        public List<(string path, object? value)> SetCalls { get; } = new();

        public List<(string operatorName, string operatorContext, BlenderContextOverride? contextOverride)> OperatorPollCalls { get; } = new();

        public List<(string operatorName, BlenderOperatorCall call)> OperatorCallRecords { get; } = new();

        public int WatchSubscribeCount { get; private set; }

        public int WatchDisposeCount { get; private set; }

        private Func<WatchDirtyEvent, Task>? _watchCallback;

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
            private readonly TestBlenderApi _owner;

            public TrackingAsyncDisposable(TestBlenderApi owner)
            {
                _owner = owner;
            }

            public ValueTask DisposeAsync()
            {
                _owner.WatchDisposeCount++;
                return ValueTask.CompletedTask;
            }
        }

        public sealed class TestBlenderRnaApi : IBlenderRnaApi
        {
            private readonly TestBlenderApi _owner;

            public TestBlenderRnaApi(TestBlenderApi owner)
            {
                _owner = owner;
            }

            public Task<IReadOnlyList<RnaItemRef>> ListAsync(string path, CancellationToken cancellationToken = default)
            {
                _owner.ListedPaths.Add(path);
                return _owner.ListAsyncImpl?.Invoke(path, cancellationToken) ?? Task.FromResult<IReadOnlyList<RnaItemRef>>(Array.Empty<RnaItemRef>());
            }

            public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default)
            {
                _owner.GetPaths.Add(path);
                return (T)(await (_owner.GetAsyncImpl?.Invoke(path, cancellationToken) ?? Task.FromResult<object?>(default(T))) ?? throw new InvalidOperationException("No value."));
            }

            public Task SetAsync<T>(string path, T value, CancellationToken cancellationToken = default)
            {
                _owner.SetCalls.Add((path, value));
                return _owner.SetAsyncImpl?.Invoke(path, value, cancellationToken) ?? Task.CompletedTask;
            }

            public Task<BlenderArrayReadResult> ReadArrayAsync(string path, CancellationToken cancellationToken = default)
            {
                _owner.ReadArrayPaths.Add(path);
                return _owner.ReadArrayAsyncImpl?.Invoke(path, cancellationToken)
                       ?? Task.FromResult(
                           new BlenderArrayReadResult
                           {
                               Path = path,
                               Count = 0,
                               RawBytes = Array.Empty<byte>(),
                           });
            }

            public Task<RnaDescribeResult> DescribeAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            public async Task<T> CallAsync<T>(string path, string method, params BlenderNamedArg[] kwargs)
            {
                return (T)(await (_owner.CallAsyncImpl?.Invoke(path, method, kwargs, CancellationToken.None) ?? Task.FromResult<object?>(default(T))) ?? throw new InvalidOperationException("No call result."));
            }

            public Task<T> CallAsync<T>(string path, string method, BlenderMethodCall call, CancellationToken cancellationToken = default)
            {
                var kwargs = call.Kwargs ?? Array.Empty<BlenderNamedArg>();
                return CallAsync<T>(path, method, kwargs.ToArray());
            }
        }

        public sealed class TestBlenderOpsApi : IBlenderOpsApi
        {
            private readonly TestBlenderApi _owner;

            public TestBlenderOpsApi(TestBlenderApi owner)
            {
                _owner = owner;
            }

            public Task<OperatorPollResult> PollAsync(string operatorName, string operatorContext = "EXEC_DEFAULT", BlenderContextOverride? contextOverride = null, CancellationToken cancellationToken = default)
            {
                _owner.OperatorPollCalls.Add((operatorName, operatorContext, contextOverride));
                return _owner.PollOperatorImpl?.Invoke(operatorName, operatorContext, contextOverride, cancellationToken)
                       ?? Task.FromResult(
                           new OperatorPollResult
                           {
                               OperatorName = operatorName,
                               CanExecute = true,
                           });
            }

            public Task<OperatorCallResult> CallAsync(string operatorName, params BlenderNamedArg[] properties)
            {
                return CallAsync(
                    operatorName,
                    new BlenderOperatorCall
                    {
                        Properties = properties,
                    });
            }

            public Task<OperatorCallResult> CallAsync(string operatorName, BlenderOperatorCall call, CancellationToken cancellationToken = default)
            {
                _owner.OperatorCallRecords.Add((operatorName, call));
                return _owner.CallOperatorImpl?.Invoke(operatorName, call, cancellationToken)
                       ?? Task.FromResult(
                           new OperatorCallResult
                           {
                               OperatorName = operatorName,
                               Result = ["FINISHED"],
                           });
            }
        }

        public sealed class TestBlenderObserveApi : IBlenderObserveApi
        {
            private readonly TestBlenderApi _owner;

            public TestBlenderObserveApi(TestBlenderApi owner)
            {
                _owner = owner;
            }

            public Task<IAsyncDisposable> WatchAsync(string watchId, WatchSource source, string path, Func<WatchDirtyEvent, Task> onDirty, CancellationToken cancellationToken = default)
            {
                _owner.WatchSubscribeCount++;
                _owner._watchCallback = onDirty;
                return Task.FromResult<IAsyncDisposable>(new TrackingAsyncDisposable(_owner));
            }

            public Task<WatchSnapshot> ReadAsync(string watchId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new WatchSnapshot
                {
                    WatchId = watchId,
                    Revision = 1,
                    Source = WatchSource.Depsgraph,
                });
            }
        }

        private sealed class ThrowingBusinessEndpoint : IBusinessEndpoint
        {
            public ValueTask<BusinessResponse> InvokeAsync(BusinessRequest request, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException("TestBlenderApi uses domain test doubles directly.");
            }
        }
    }
}
