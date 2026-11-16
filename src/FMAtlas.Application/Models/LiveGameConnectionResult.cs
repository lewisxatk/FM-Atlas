namespace FMAtlas.Application.Models;

public sealed record LiveGameConnectionResult(
    int ProcessId,
    string BuildName,
    string ProviderKey,
    long SaveGeneration,
    int PlayersImported,
    int StaffImported,
    int ClubsImported,
    bool LiveWritesEnabled,
    string Message);
