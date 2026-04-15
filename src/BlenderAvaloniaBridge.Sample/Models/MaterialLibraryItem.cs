using Avalonia.Media;
using BlenderAvaloniaBridge;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderAvaloniaBridge.Sample.Models;

public partial class MaterialLibraryItem : ObservableObject, IDisposable
{
    public MaterialLibraryItem(RnaItemRef materialRef, string displayName, IImage? thumbnail)
    {
        MaterialRef = materialRef;
        _displayName = displayName;
        _thumbnail = thumbnail;
    }

    public RnaItemRef MaterialRef { get; private set; }

    [ObservableProperty]
    private string _displayName;

    public IImage? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (ReferenceEquals(_thumbnail, value))
            {
                return;
            }

            var previous = _thumbnail;
            if (SetProperty(ref _thumbnail, value))
            {
                if (!ReferenceEquals(previous, value) && previous is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                OnPropertyChanged(nameof(HasThumbnail));
                OnPropertyChanged(nameof(ShowPlaceholder));
            }
        }
    }

    private IImage? _thumbnail;

    public bool HasThumbnail => Thumbnail is not null;

    public bool ShowPlaceholder => Thumbnail is null;

    public string KindLabel => string.IsNullOrWhiteSpace(MaterialRef.IdType) ? "Material" : MaterialRef.IdType!;

    public string DisplayInitials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                return "MT";
            }

            var tokens = DisplayName
                .Split([' ', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (tokens.Length == 0)
            {
                return DisplayName[..Math.Min(2, DisplayName.Length)].ToUpperInvariant();
            }

            if (tokens.Length == 1)
            {
                return tokens[0][..Math.Min(2, tokens[0].Length)].ToUpperInvariant();
            }

            return string.Concat(tokens[0][0], tokens[1][0]).ToUpperInvariant();
        }
    }

    public void UpdateReference(RnaItemRef materialRef)
    {
        MaterialRef = materialRef;
        OnPropertyChanged(nameof(MaterialRef));
        OnPropertyChanged(nameof(KindLabel));
    }

    public void Dispose()
    {
        if (_thumbnail is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _thumbnail = null;
        OnPropertyChanged(nameof(Thumbnail));
        OnPropertyChanged(nameof(HasThumbnail));
        OnPropertyChanged(nameof(ShowPlaceholder));
    }
}
