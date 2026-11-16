using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FMAtlas.Desktop.ViewModels;

/// <summary>Presentation item for a compact profile section tab.</summary>
public sealed class ProfileSectionTabViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public ProfileSectionTabViewModel(string name, bool isSelected = false)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A section name is required.", nameof(name)) : name;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Name { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs());
        }
    }
}
