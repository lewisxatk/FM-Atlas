using System.Globalization;
using System.Text;
using FmAtlas.Data;
using FmAtlas.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FmAtlas.Tests;

public sealed class FmViewExportSnapshotScopeTests
{
    [Fact]
    public async Task ViewExportMetricsAreScopedToTheExactSaveSnapshotTheyWereMatchedAgainst()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas.ViewExportSnapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databasePath = Path.Combine(folder, "scouting.db");
        var firstCsv = Path.Combine(folder, "career-one.csv");
        var secondCsv = Path.Combine(folder, "career-two.csv");

        try
        {
            var database = new ScoutDatabase(databasePath, pooling: false);
            await database.InitialiseAsync();

            await using (var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false, ForeignKeys = true }.ToString()))
            {
                await connection.OpenAsync();

                async Task AddSnapshotPlayerAsync(string fileName, string uid, string sourceId)
                {
                    var snapshot = connection.CreateCommand();
                    snapshot.CommandText = """
                        INSERT INTO FmsaveImportRuns(SourceFileName,SourceFileSize,CreatedUtc)
                        VALUES($file,1,$created);
                        """;
                    snapshot.Parameters.AddWithValue("$file", fileName);
                    snapshot.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    await snapshot.ExecuteNonQueryAsync();

                    var snapshotId = Convert.ToInt32(await new SqliteCommand("SELECT last_insert_rowid();", connection).ExecuteScalarAsync(), CultureInfo.InvariantCulture);

                    var player = connection.CreateCommand();
                    player.CommandText = """
                        INSERT INTO Players(SourceId,Name,Age,Club,League,Position,CurrentAbility,PotentialAbility,Value,DataOrigin,UpdatedUtc)
                        VALUES($id,$name,22,'Test FC','Test League','ST',120,150,1000000,'fmsave','');
                        """;
                    player.Parameters.AddWithValue("$id", sourceId);
                    player.Parameters.AddWithValue("$name", "Player " + uid);
                    await player.ExecuteNonQueryAsync();

                    var mapping = connection.CreateCommand();
                    mapping.CommandText = """
                        INSERT INTO FmsavePlayerSnapshots(SnapshotId,PlayerUid,PlayerSourceId,RawJson)
                        VALUES($snapshot,$uid,$id,'{}');
                        """;
                    mapping.Parameters.AddWithValue("$snapshot", snapshotId);
                    mapping.Parameters.AddWithValue("$uid", uid);
                    mapping.Parameters.AddWithValue("$id", sourceId);
                    await mapping.ExecuteNonQueryAsync();
                }

                await AddSnapshotPlayerAsync("career-one.fm", "101", "FMSAVE:career-one:101");
            }

            await File.WriteAllTextAsync(firstCsv,
                "Unique ID,Player,Club,Minutes,CareerOneOnlyMetric\n101,Player 101,Test FC,900,41\n",
                new UTF8Encoding(true));
            var firstImport = await database.ImportFmViewExporterCsvAsync(firstCsv);
            Assert.Equal(1, firstImport.RowsImported);

            await using (var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false, ForeignKeys = true }.ToString()))
            {
                await connection.Open();
                var snapshot = connection.CreateCommand();
                snapshot.CommandText = """
                    INSERT INTO FmsaveImportRuns(SourceFileName,SourceFileSize,CreatedUtc)
                    VALUES('career-two.fm',1,$created);
                    """;
                snapshot.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                await snapshot.ExecuteNonQueryAsync();
                var secondSnapshotId = Convert.ToInt32(await new SqliteCommand("SELECT last_insert_rowid();", connection).ExecuteScalarAsync(), CultureInfo.InvariantCulture);

                var player = connection.CreateCommand();
                player.CommandText = """
                    INSERT INTO Players(SourceId,Name,Age,Club,League,Position,CurrentAbility,PotentialAbility,Value,DataOrigin,UpdatedUtc)
                    VALUES('FMSAVE:career-two:202','Player 202',22,'Test FC','Test League','ST',120,150,1000000,'fmsave','');
                    """;
                await player.ExecuteNonQueryAsync();

                var mapping = connection.CreateCommand();
                mapping.CommandText = """
                    INSERT INTO FmsavePlayerSnapshots(SnapshotId,PlayerUid,PlayerSourceId,RawJson)
                    VALUES($snapshot,'202','FMSAVE:career-two:202','{}');
                    """;
                mapping.Parameters.AddWithValue("$snapshot", secondSnapshotId);
                await mapping.ExecuteNonQueryAsync();
            }

            await File.WriteAllTextAsync(secondCsv,
                "Unique ID,Player,Club,Minutes,CareerTwoOnlyMetric\n202,Player 202,Test FC,900,73\n",
                new UTF8Encoding(true));
            var secondImport = await database.ImportFmViewExporterCsvAsync(secondCsv);
            Assert.Equal(1, secondImport.RowsImported);

            var metricCatalog = await database.GetSeasonMetricCatalogAsync();
            Assert.Contains(metricCatalog, metric => metric.Key.Equals("CareerTwoOnlyMetric", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(metricCatalog, metric => metric.Key.Equals("CareerOneOnlyMetric", StringComparison.OrdinalIgnoreCase));

            var oldCareerFacts = await database.GetPlayerPerformanceFactsAsync("FMSAVE:career-one:101");
            Assert.DoesNotContain(oldCareerFacts, fact => fact.Field.Contains("CareerOneOnlyMetric", StringComparison.OrdinalIgnoreCase));

            var activeCareerFacts = await database.GetPlayerPerformanceFactsAsync("FMSAVE:career-two:202");
            Assert.Contains(activeCareerFacts, fact => fact.Field.Contains("CareerTwoOnlyMetric", StringComparison.OrdinalIgnoreCase));

            var stalePlayer = await database.GetPlayerBySourceIdAsync("FMSAVE:career-one:101");
            Assert.Null(stalePlayer);
            var activePlayer = await database.GetPlayerBySourceIdAsync("FMSAVE:career-two:202");
            Assert.NotNull(activePlayer);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }
}
