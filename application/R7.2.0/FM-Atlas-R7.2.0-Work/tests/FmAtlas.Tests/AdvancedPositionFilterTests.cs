using System.Text.Json;
using Microsoft.Data.Sqlite;
using FmAtlas.Data;
using FmAtlas.Models;
using Xunit;

namespace FmAtlas.Tests;

public sealed class AdvancedPositionFilterTests
{
    [Fact]
    public void ZeroRatingsAreNoRequirementAndEmptyFilterNormalizesToNull()
    {
        Assert.Null(AdvancedPositionFilterRules.Normalize(null));
        Assert.Null(AdvancedPositionFilterRules.Normalize(new AdvancedPositionFilter(
            Positions: new[] { new AdvancedPositionCriterion("GK", 0), new AdvancedPositionCriterion("DC", 0) })));
    }

    [Fact]
    public void NormalizesLegacyStrikerAliasAndKeepsExactAllSemantics()
    {
        var result = AdvancedPositionFilterRules.Normalize(new AdvancedPositionFilter(
            MatchAny: false,
            AtLeast: false,
            Positions: new[]
            {
                new AdvancedPositionCriterion("ST", 16),
                new AdvancedPositionCriterion("DC", 14)
            }));

        Assert.NotNull(result);
        Assert.False(result.MatchAny);
        Assert.False(result.AtLeast);
        Assert.Equal(new[] { "DC", "STC" }, result.Positions!.Select(x => x.PositionCode));
        Assert.Equal(new[] { 14, 16 }, result.Positions.Select(x => x.Rating));
    }

    [Fact]
    public void PreservesAnyAndAtLeastOptionsForSelectedPositions()
    {
        var result = AdvancedPositionFilterRules.Normalize(new AdvancedPositionFilter(
            MatchAny: true,
            AtLeast: true,
            Positions: new[] { new AdvancedPositionCriterion("AML", 12) }));

        Assert.NotNull(result);
        Assert.True(result.MatchAny);
        Assert.True(result.AtLeast);
        Assert.Equal(12, Assert.Single(result.Positions!).Rating);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void RejectsPositionRatingsOutsideZeroToTwenty(int rating)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AdvancedPositionFilterRules.Normalize(new AdvancedPositionFilter(
                Positions: new[] { new AdvancedPositionCriterion("DC", rating) })));
    }

    [Fact]
    public void RejectsUnknownPositionCodes()
    {
        Assert.Throws<ArgumentException>(() =>
            AdvancedPositionFilterRules.Normalize(new AdvancedPositionFilter(
                Positions: new[] { new AdvancedPositionCriterion("CB", 15) })));
    }
    [Fact]
    public void OlderSavedProfilesWithoutPositionFilterRemainLoadable()
    {
        const string json = """
            {
              "Id": "legacy",
              "Name": "Legacy search",
              "UpdatedUtc": "2026-11-14T00:00:00Z",
              "SearchText": "",
              "MatchAll": true,
              "Criteria": []
            }
            """;

        var profile = JsonSerializer.Deserialize<AdvancedSearchProfile>(json);

        Assert.NotNull(profile);
        Assert.Null(profile.PositionFilter);
    }

    [Fact]
    public void PositionSettingsRoundTripInsideSavedProfile()
    {
        var profile = new AdvancedSearchProfile(
            "position-only",
            "Any natural winger",
            DateTime.UtcNow,
            "",
            true,
            Array.Empty<AdvancedSearchCriterion>(),
            PositionFilter: new AdvancedPositionFilter(
                MatchAny: true,
                AtLeast: true,
                Positions: new[] { new AdvancedPositionCriterion("AML", 18), new AdvancedPositionCriterion("AMR", 18) }));

        var json = JsonSerializer.Serialize(profile);
        var loaded = JsonSerializer.Deserialize<AdvancedSearchProfile>(json);

        Assert.NotNull(loaded?.PositionFilter);
        Assert.True(loaded.PositionFilter.MatchAny);
        Assert.True(loaded.PositionFilter.AtLeast);
        Assert.Equal(new[] { "AML", "AMR" }, loaded.PositionFilter.Positions!.Select(x => x.PositionCode));
    }

    [Fact]
    public async Task DatabaseSearchAppliesExactAllAnyAndAtLeastToOfflineAndLivePositionFacts()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "fmatlas-position-search-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var database = new ScoutDatabase(databasePath);
            await database.InitialiseAsync();

            await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString()))
            {
                await connection.OpenAsync();
                var snapshot = connection.CreateCommand();
                snapshot.CommandText = """
                    INSERT INTO FmsaveImportRuns(SourceFileName,SourceFileSize,CreatedUtc)
                    VALUES('position-test.fm',1,'2026-11-14T00:00:00Z');
                    SELECT last_insert_rowid();
                    """;
                var snapshotId = Convert.ToInt32(await snapshot.ExecuteScalarAsync());

                async Task AddOfflinePlayer(string sourceId, string name, double dc, double stc, string uid)
                {
                    var player = connection.CreateCommand();
                    player.CommandText = "INSERT INTO Players(SourceId,Name,DataOrigin,UpdatedUtc) VALUES($id,$name,'fmsave','');";
                    player.Parameters.AddWithValue("$id", sourceId);
                    player.Parameters.AddWithValue("$name", name);
                    await player.ExecuteNonQueryAsync();

                    var raw = connection.CreateCommand();
                    raw.CommandText = "INSERT INTO FmsavePlayerSnapshots(SnapshotId,PlayerUid,PlayerSourceId,RawJson) VALUES($snapshot,$uid,$id,'{}');";
                    raw.Parameters.AddWithValue("$snapshot", snapshotId);
                    raw.Parameters.AddWithValue("$uid", uid);
                    raw.Parameters.AddWithValue("$id", sourceId);
                    await raw.ExecuteNonQueryAsync();

                    foreach (var (code, rating) in new[] { ("dc", dc), ("stc", stc) })
                    {
                        var fact = connection.CreateCommand();
                        fact.CommandText = "INSERT INTO FmsavePlayerFacts(SnapshotId,PlayerSourceId,FactGroup,FieldKey,NumericValue) VALUES($snapshot,$id,'positions',$field,$rating);";
                        fact.Parameters.AddWithValue("$snapshot", snapshotId);
                        fact.Parameters.AddWithValue("$id", sourceId);
                        fact.Parameters.AddWithValue("$field", code);
                        fact.Parameters.AddWithValue("$rating", rating);
                        await fact.ExecuteNonQueryAsync();
                    }
                }

                await AddOfflinePlayer("FMSAVE:test:alpha", "Alpha", 15, 18, "1");
                await AddOfflinePlayer("FMSAVE:test:beta", "Beta", 17, 19, "2");
                await AddOfflinePlayer("FMSAVE:test:gamma", "Gamma", 14, 18, "3");

                var liveRun = connection.CreateCommand();
                liveRun.CommandText = """
                    INSERT INTO LiveImportRuns(GameVersion,ModuleSha256,StartedUtc,Status)
                    VALUES('FM27','test-hash','2026-11-14T00:00:00Z','Complete');
                    SELECT last_insert_rowid();
                    """;
                var liveRunId = Convert.ToInt32(await liveRun.ExecuteScalarAsync());

                var livePlayer = connection.CreateCommand();
                livePlayer.CommandText = "INSERT INTO LivePlayers(PlayerUid,PlayerName,SourceRunId,UpdatedUtc) VALUES(42,'Delta',$run,'2026-11-14T00:00:00Z');";
                livePlayer.Parameters.AddWithValue("$run", liveRunId);
                await livePlayer.ExecuteNonQueryAsync();

                var liveAtlasPlayer = connection.CreateCommand();
                liveAtlasPlayer.CommandText = "INSERT INTO Players(SourceId,Name,DataOrigin,UpdatedUtc) VALUES('LIVE:42','Delta','FM27 Live','');";
                await liveAtlasPlayer.ExecuteNonQueryAsync();

                foreach (var (code, rating) in new[] { ("dc", 15d), ("stc", 18d) })
                {
                    var fact = connection.CreateCommand();
                    fact.CommandText = "INSERT INTO LivePlayerFacts(PlayerUid,FieldKey,NumericValue,ValidationStatus,SourceRunId,UpdatedUtc) VALUES(42,$field,$rating,'Candidate',$run,'2026-11-14T00:00:00Z');";
                    fact.Parameters.AddWithValue("$field", "positions." + code);
                    fact.Parameters.AddWithValue("$rating", rating);
                    fact.Parameters.AddWithValue("$run", liveRunId);
                    await fact.ExecuteNonQueryAsync();
                }
            }

            var exactAll = await database.SearchAdvancedPlayersAsync(
                "", Array.Empty<AdvancedSearchCriterion>(), true, 50, 0, default, null,
                new AdvancedPositionFilter(false, false, new[]
                {
                    new AdvancedPositionCriterion("DC", 15),
                    new AdvancedPositionCriterion("STC", 18)
                }));
            Assert.Equal(new[] { "Alpha", "Delta" }, exactAll.Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));

            var exactAny = await database.SearchAdvancedPlayersAsync(
                "", Array.Empty<AdvancedSearchCriterion>(), true, 50, 0, default, null,
                new AdvancedPositionFilter(true, false, new[]
                {
                    new AdvancedPositionCriterion("DC", 15),
                    new AdvancedPositionCriterion("STC", 18)
                }));
            Assert.Equal(new[] { "Alpha", "Delta", "Gamma" }, exactAny.Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));

            var atLeastAll = await database.SearchAdvancedPlayersAsync(
                "", Array.Empty<AdvancedSearchCriterion>(), true, 50, 0, default, null,
                new AdvancedPositionFilter(false, true, new[]
                {
                    new AdvancedPositionCriterion("DC", 15),
                    new AdvancedPositionCriterion("STC", 18)
                }));
            Assert.Equal(new[] { "Alpha", "Beta", "Delta" }, atLeastAll.Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                try { File.Delete(databasePath + suffix); } catch { }
        }
    }

}
