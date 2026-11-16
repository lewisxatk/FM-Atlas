using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FMAtlas.Application.Abstractions;
using FMAtlas.Domain.Connection;
using FMAtlas.Desktop.Commands;

namespace FMAtlas.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IGameProcessProbe _processProbe;
    private string _connectionStatus = "Not connected";
    private string _connectionDetail = "Load Game checks for the FM process and game module only. Career data loading is still in progress.";
    private string _lastAction = "Fresh application foundation";
    private bool _isBusy;

    public MainWindowViewModel(IGameProcessProbe processProbe)
    {
        _processProbe = processProbe;
        LoadGameCommand = new AsyncRelayCommand(ProbeGameAsync, () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand LoadGameCommand { get; }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetField(ref _connectionStatus, value);
    }

    public string ConnectionDetail
    {
        get => _connectionDetail;
        private set => SetField(ref _connectionDetail, value);
    }

    public string LastAction
    {
        get => _lastAction;
        private set => SetField(ref _lastAction, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task ProbeGameAsync()
    {
        IsBusy = true;
        ConnectionStatus = "Checking game process";
        ConnectionDetail = "Checking for Football Manager and its game module. No memory values will be read or changed in this step.";
        LastAction = "Checking game process…";

        try
        {
            var result = await _processProbe.ProbeAsync();

            switch (result.State)
            {
                case GameProcessProbeState.UnsupportedPlatform:
                    ConnectionStatus = "Windows required";
                    ConnectionDetail = "Live Football Manager process detection is available in the Windows desktop build.";
                    LastAction = "Process check unavailable on this platform";
                    break;

                case GameProcessProbeState.NotFound:
                    ConnectionStatus = "Not detected";
                    ConnectionDetail = "No FM process was found. Start Football Manager 27 and load a career, then try again.";
                    LastAction = "No Football Manager process found";
                    break;

                case GameProcessProbeState.ProcessFoundModuleUnconfirmed:
                    ConnectionStatus = "Process found";
                    ConnectionDetail = "An FM process was found, but game_plugin.dll could not be confirmed. Build verification and career loading remain In Progress.";
                    LastAction = "Process detected; module not confirmed";
                    break;

                case GameProcessProbeState.ProcessAndModuleFound:
                    var candidate = result.Candidates.First(x => x.HasGamePluginModule);
                    ConnectionStatus = "Game module detected";
                    var buildReference = candidate.ModuleFingerprint is null
                        ? null
                        : GameBuildProfileCatalog.MatchReference(candidate.ModuleFingerprint);
                    ConnectionDetail = candidate.ModuleFingerprint is null
                        ? $"FM process {candidate.ProcessId} and game_plugin.dll were detected, but the static module fingerprint could not be read. Exact build support, career identity and live data import remain In Progress. No player data has been read."
                        : buildReference is null
                            ? $"FM process {candidate.ProcessId} and game_plugin.dll were detected. Static module metadata: {candidate.ModuleFingerprint.Summary}. No known reference profile matches; no live data has been read or written."
                            : $"FM process {candidate.ProcessId} matches the external reference fingerprint for {buildReference.DisplayName}. {candidate.ModuleFingerprint.Summary}. This is reference-only evidence, not an FM Atlas verified build. Live reads and writes remain disabled.";
                    LastAction = $"Detected FM process {candidate.ProcessId}; build fingerprint checked, live editing disabled";
                    break;
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = "Check failed";
            ConnectionDetail = $"The process check failed: {ex.Message}";
            LastAction = "Process check failed";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
