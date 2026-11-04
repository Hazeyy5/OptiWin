using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OptiWin.Models;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnChanged(name);
        return true;
    }

    protected void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Une optimisation réversible.</summary>
public class Tweak : Observable
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public bool Recommended { get; init; }
    public bool RequiresRestart { get; init; }
    public required Func<bool> Check { get; init; }
    public required Action ApplyAction { get; init; }
    public required Action RevertAction { get; init; }

    public string Badge => RequiresRestart ? "Redémarrage requis" : "";
    public bool HasBadge => RequiresRestart;

    bool _isApplied;
    public bool IsApplied { get => _isApplied; set => Set(ref _isApplied, value); }

    bool _isBusy;
    public bool IsBusy { get => _isBusy; set { Set(ref _isBusy, value); OnChanged(nameof(IsIdle)); } }
    public bool IsIdle => !_isBusy;

    public void Refresh()
    {
        try { IsApplied = Check(); } catch { IsApplied = false; }
    }
}

/// <summary>Une catégorie de fichiers à nettoyer.</summary>
public class CleanupCategory : Observable
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Func<long> Measure { get; init; }
    public required Func<long> Clean { get; init; }

    bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    long _size = -1;
    public long Size { get => _size; set { Set(ref _size, value); OnChanged(nameof(SizeText)); } }
    public string SizeText => _size < 0 ? "—" : Services.Format.Bytes(_size);
}

/// <summary>Un programme lancé au démarrage de Windows.</summary>
public class StartupItem : Observable
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required string Location { get; init; }
    public required Microsoft.Win32.RegistryHive ApprovedHive { get; init; }
    public required string ApprovedPath { get; init; }

    bool _isEnabled;
    public bool IsEnabled { get => _isEnabled; set => Set(ref _isEnabled, value); }
}
