using FmAtlas.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FmAtlas.Tests;

public sealed class LivePlayerSearchIntegrationTests
{
    [Fact]
    public async Task LivePlayerCAandSignedPAAreSearchableAndProfileAttributesRemainAvailable()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas.LivePlayerSearch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databasePath = Path.Combine(folder, "scouting.db");

        try
        {
            var database = new ScoutDatabase(databasePath);
            await database.InitialiseAsync();

            await using (var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString()))
            {
                await connection.OpenAsync();

                var player = connection.CreateCommand();
                player.CommandText = """
                    INSERT INTO Players(SourceId,Name,Age,Club,Nation,Position,CurrentAbility,PotentialAbility,DataOrigin,UpdatedUtc)
                    VALUES('LIVE:200','Live Search Prospect',22,'Atlas FC','England','ST',145,-8,'FM27 live identity import','2026-11-14T00:00:00Z');
                    """;
                await player.ExecuteNonQueryAsync();

                var attribute = connection.CreateCommand();
                attribute.CommandText = """
                    INSERT INTO PlayerAttributes(SourceId,AttributeName,Category,AttributeValue,UpdatedUtc)
                    VALUES('LIVE:200','Finishing','Technical',17,'2026-11-14T00:00:00Z');
                    """;
                await attribute.ExecuteNonQueryAsync();
            }

            var result = await database.SearchAsync(
                search: "Live Search Prospect",
                minAge: null,
                maxAge: null,
                position: null,
                league: null,
                nation: "England",
                homeGrown: null,
                minCa: 140,
                maxCa: 160,
                minPa: -10,
                maxPa: -1,
                limit: 100);

            var match = Assert.Single(result);
            Assert.Equal("LIVE:200", match.SourceId);
            Assert.Equal(145, match.CurrentAbility);
            Assert.Equal(-8, match.PotentialAbility);

            var profileFacts = await database.GetPlayerAttributesAsync(match.SourceId);
            Assert.Equal(17d, profileFacts["Finishing"]);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
