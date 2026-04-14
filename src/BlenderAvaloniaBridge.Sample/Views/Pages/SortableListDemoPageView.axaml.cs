using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BlenderAvaloniaBridge.Sample.ViewModels;

namespace BlenderAvaloniaBridge.Sample.Views.Pages;

public sealed partial class SortableListDemoPageView : UserControl
{
    private readonly ListBox? _sortableListBox;
    private bool _captured;
    private bool _dragStarted;
    private Point _start;
    private int _draggedIndex = -1;
    private int _targetIndex = -1;
    private IPointer? _capturedPointer;
    private SortableListItemViewModel? _draggedItem;
    private ListBoxItem? _draggedContainer;

    public SortableListDemoPageView()
    {
        InitializeComponent();
        _sortableListBox = this.FindControl<ListBox>("SortableListBox");
        AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel);
    }

    private void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            sender is not Control control ||
            control.DataContext is not SortableListItemViewModel item ||
            _sortableListBox is null)
        {
            return;
        }

        _draggedItem = item;
        _draggedContainer = control.FindAncestorOfType<ListBoxItem>()
            ?? _sortableListBox.ContainerFromItem(item) as ListBoxItem;
        if (_draggedContainer is null)
        {
            return;
        }

        _draggedIndex = _sortableListBox.IndexFromContainer(_draggedContainer);
        if (_draggedIndex < 0)
        {
            ResetDragState();
            return;
        }

        _targetIndex = -1;
        _start = e.GetPosition(_sortableListBox);
        _dragStarted = false;
        SetDraggingPseudoClasses(_draggedContainer, true);
        _draggedContainer.SetValue(Visual.ZIndexProperty, 1000);
        _capturedPointer = e.Pointer;
        _capturedPointer.Capture(_draggedContainer);
        _captured = true;
        AddTransforms();
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if (_captured && !properties.IsLeftButtonPressed)
        {
            FinishDrag();
            return;
        }

        if (!_captured || !properties.IsLeftButtonPressed || _sortableListBox?.Items is null || _draggedContainer is null)
        {
            return;
        }

        var position = e.GetPosition(_sortableListBox);
        var delta = position.Y - _start.Y;
        if (!_dragStarted)
        {
            if (Math.Abs(delta) < 3)
            {
                return;
            }

            _dragStarted = true;
        }

        SetTranslateTransform(_draggedContainer, 0, delta);
        _draggedIndex = _sortableListBox.IndexFromContainer(_draggedContainer);
        _targetIndex = -1;

        var draggedBounds = _draggedContainer.Bounds;
        var draggedStart = draggedBounds.Y;
        var draggedDeltaStart = draggedBounds.Y + delta;
        var draggedDeltaEnd = draggedBounds.Y + delta + draggedBounds.Height;

        for (var index = 0; index < _sortableListBox.ItemCount; index++)
        {
            if (_sortableListBox.ContainerFromIndex(index) is not ListBoxItem targetContainer ||
                ReferenceEquals(targetContainer, _draggedContainer))
            {
                continue;
            }

            var targetBounds = targetContainer.Bounds;
            var targetStart = targetBounds.Y;
            var targetMid = targetBounds.Y + targetBounds.Height / 2;

            if (targetStart > draggedStart && draggedDeltaEnd >= targetMid)
            {
                SetTranslateTransform(targetContainer, 0, -draggedBounds.Height);
                _targetIndex = _targetIndex == -1 ? index : Math.Max(_targetIndex, index);
            }
            else if (targetStart < draggedStart && draggedDeltaStart <= targetMid)
            {
                SetTranslateTransform(targetContainer, 0, draggedBounds.Height);
                _targetIndex = _targetIndex == -1 ? index : Math.Min(_targetIndex, index);
            }
            else
            {
                SetTranslateTransform(targetContainer, 0, 0);
            }
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_captured && e.InitialPressMouseButton == MouseButton.Left)
        {
            FinishDrag();
            e.Handled = true;
        }
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        FinishDrag();
    }

    private void FinishDrag()
    {
        try
        {
            var shouldMove =
                _dragStarted &&
                _sortableListBox is not null &&
                DataContext is SortableListDemoPageViewModel &&
                _draggedItem is not null &&
                _draggedIndex >= 0 &&
                _targetIndex >= 0 &&
                _draggedIndex != _targetIndex;

            ResetTransformsWithoutTransition();

            if (shouldMove &&
                _sortableListBox is not null &&
                DataContext is SortableListDemoPageViewModel viewModel &&
                _draggedItem is not null)
            {
                viewModel.MoveItemToIndex(_draggedItem, _targetIndex);
            }
        }
        finally
        {
            if (_draggedContainer is not null)
            {
                SetDraggingPseudoClasses(_draggedContainer, false);
                _draggedContainer.SetValue(Visual.ZIndexProperty, 0);
            }

            ReleasePointerCapture();
            ResetDragState();
            Dispatcher.UIThread.Post(RestoreTransformsTransition, DispatcherPriority.Background);
        }
    }

    private void AddTransforms()
    {
        if (_sortableListBox?.Items is null)
        {
            return;
        }

        for (var index = 0; index < _sortableListBox.ItemCount; index++)
        {
            if (_sortableListBox.ContainerFromIndex(index) is ListBoxItem container)
            {
                SetTranslateTransform(container, 0, 0);
            }
        }
    }

    private void ResetTransforms()
    {
        if (_sortableListBox?.Items is null)
        {
            return;
        }

        for (var index = 0; index < _sortableListBox.ItemCount; index++)
        {
            if (_sortableListBox.ContainerFromIndex(index) is ListBoxItem container)
            {
                SetTranslateTransform(container, 0, 0);
            }
        }
    }

    private void ResetTransformsWithoutTransition()
    {
        if (_sortableListBox?.Items is null)
        {
            return;
        }

        for (var index = 0; index < _sortableListBox.ItemCount; index++)
        {
            if (_sortableListBox.ContainerFromIndex(index) is ListBoxItem container)
            {
                container.Transitions = null;
                SetTranslateTransform(container, 0, 0);
            }
        }
    }

    private void RestoreTransformsTransition()
    {
        if (_sortableListBox?.Items is null)
        {
            return;
        }

        for (var index = 0; index < _sortableListBox.ItemCount; index++)
        {
            if (_sortableListBox.ContainerFromIndex(index) is ListBoxItem container)
            {
                container.ClearValue(TransitionsProperty);
            }
        }
    }

    private void ReleasePointerCapture()
    {
        if (_capturedPointer is not null)
        {
            _capturedPointer.Capture(null);
        }

        _capturedPointer = null;
    }

    private void ResetDragState()
    {
        _captured = false;
        _dragStarted = false;
        _draggedIndex = -1;
        _targetIndex = -1;
        _draggedItem = null;
        _draggedContainer = null;
    }

    private static void SetTranslateTransform(Control control, double x, double y)
    {
        var transformBuilder = new TransformOperations.Builder(1);
        transformBuilder.AppendTranslate(x, y);
        control.RenderTransform = transformBuilder.Build();
    }

    private static void SetDraggingPseudoClasses(Control control, bool isDragging)
    {
        if (isDragging)
        {
            ((IPseudoClasses)control.Classes).Add(":dragging");
        }
        else
        {
            ((IPseudoClasses)control.Classes).Remove(":dragging");
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
