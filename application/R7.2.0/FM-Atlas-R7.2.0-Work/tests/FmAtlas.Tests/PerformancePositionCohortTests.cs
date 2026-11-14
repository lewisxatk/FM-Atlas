using Microsoft.Data.Sqlite;
using FmAtlas.Data;
using Xunit;

namespace FmAtlas.Tests;

public sealed class PerformancePositionCohortTests
{
    [Fact]
    public async Task PerformanceValueAnalysisCanComparePlayersByNumericFmPositionFamiliarity()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "fmatlas-scouting-position-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var database = new ScoutDatabase(databasePath);
            await database.InitialiseAsync();

            await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString()))
            {
                await connection.OpenAsync();
                var snapshot = connection.CreateCommand();
                snapshot.CommandText = "INSERT INTO FmsaveImportRuns(SourceFileName,SourceFileSize,CreatedUtc) VALUES('scouting-position-test.fm',1,'2026-11-14T00:00:00Z');";
                await snapshot.ExecuteNonQueryAsync();
                var idCommand = connection.CreateCommand();
                idCommand.CommandText = "SELECT last_insert_rowid();";
                var snapshotId = Convert.ToInt32(await idCommand.ExecuteScalarAsync());

                async Task AddPlayer(string uid, string name, string position, int rating, double goals, double value)
                {
                    var sourceId = "FMSAVE:scouting:" + uid;
                    var player = connection.CreateCommand();
                    player.CommandText = "INSERT INTO Players(SourceId,Name,Age,Club,League,Position,Value,DataOrigin,UpdatedUtc) VALUES($id,$name,22,'Club','Test League',$position,$value,'fmsave','');";
                    player.Parameters.AddWithValue("$id", sourceId);
                    player.Parameters.AddWithValue("$name", name);
                    player.Parameters.AddWithValue("$position", position);
                    player.Parameters.AddWithValue("$value", value);
                    await player.ExecuteNonQueryAsync();

                    var playerSnapshot = connection.CreateCommand();
                    playerSnapshot.CommandText = "INSERT INTO FmsavePlayerSnapshots(SnapshotId,PlayerUid,PlayerSourceId,RawJson) VALUES($snapshot,$uid,$id,'{}');";
                    playerSnapshot.Parameters.AddWithValue("$snapshot", snapshotId);
                    playerSnapshot.Parameters.AddWithValue("$uid", uid);
                    playerSnapshot.Parameters.AddWithValue("$id", sourceId);
                    await playerSnapshot.ExecuteNonQueryAsync();

                    var positionFact = connection.CreateCommand();
                    positionFact.CommandText = "INSERT INTO FmsavePlayerFacts(SnapshotId,PlayerSourceId,FactGroup,FieldKey,NumericValue) VALUES($snapshot,$id,'positions',$position,$rating);";
                    positionFact.Parameters.AddWithValue("$snapshot", snapshotId);
                    positionFact.Parameters.AddWithValue("$id", sourceId);
                    positionFact.Parameters.AddWithValue("$position", position.ToLowerInvariant());
                    positionFact.Parameters.AddWithValue("$rating", rating);
                    await positionFact.ExecuteNonQueryAsync();

                    var stat = connection.CreateCommand();
                    stat.CommandText = "INSERT INTO FmsaveStatFacts(SnapshotId,TableName,RowIndex,PlayerUid,PeriodKey,Minutes,FieldKey,NumericValue) VALUES($snapshot,'player_season_stats',$row,$uid,'2026/27',900,'goals',$goals);";
                    stat.Parameters.AddWithValue("$snapshot", snapshotId);
                    stat.Parameters.AddWithValue("$row", int.Parse(uid));
                    stat.Parameters.AddWithValue("$uid", uid);
                    stat.Parameters.AddWithValue("$goals", goals);
                    await stat.ExecuteNonQueryAsync();
                }

                await AddPlayer("1", "AML Natural", "AML", 18, 10, 1000000);
                await AddPlayer("2", "AML Developing", "AML", 12, 20, 2000000);
                await AddPlayer("3", "AMR Natural", "AMR", 17, 15, 1500000);
            }

            var candidates = await database.GetPerformanceValueCandidatesAsync(
                "goals", per90: true, minimumMinutes: 450, limit: 100,
                positionRatingCode: "AML", minimumPositionRating: 15);

            var candidate = Assert.Single(candidates);
            Assert.Equal("AML Natural", candidate.Player.Name);
            Assert.Equal("AML", candidate.PositionRatingCode);
            Assert.Equal(18, candidate.PositionRating);
            Assert.Equal(1, candidate.SampleSize);
            Assert.Equal(1_000_000m, candidate.Player.Value);

            var unrestricted = await database.GetPerformanceValueCandidatesAsync(
                "goals", per90: true, minimumMinutes: 450, limit: 100);
            Assert.Equal(3, unrestricted.Count);
            Assert.All(unrestricted, item => Assert.Null(item.PositionRating));
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
            if (File.Exists(databasePath + "-wal")) File.Delete(databasePath + "-wal");
            if (File.Exists(databasePath + "-shm")) File.Delete(databasePath + "-shm");
        }
    }
}
