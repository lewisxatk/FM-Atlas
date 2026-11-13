using System.IO;
using FmAtlas.Data;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class MoneyballMetricQueryTests
{
    [TestMethod]
    public async Task MetricQueries_FallBackToLatestExternalExport_AndSupportMoreThanOneThousandRows()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas-Moneyball-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databasePath = Path.Combine(folder, "scouting.db");

        try
        {
            var database = new ScoutDatabase(databasePath, pooling: false);
            await database.InitialiseAsync();

            await using (var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString()))
            {
                await connection.OpenAsync();

                var import = connection.CreateCommand();
                import.CommandText = """
                    INSERT INTO ExternalStatImportRuns(FileName,ImportedUtc,RowsSeen,RowsImported,Notes)
                    VALUES('fm-view-export.csv','2026-11-13T00:00:00Z',1005,1005,'test fixture');
                    SELECT last_insert_rowid();
                    """;
                var importId = Convert.ToInt32(await import.ExecuteScalarAsync());

                for (var index = 0; index < 1005; index++)
                {
                    var identity = "player-" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
                    var sourceId = "FMVIEW:" + identity;
                    var value = index + 1;
                    var minutes = index == 0 ? 100 : 900;
                    var raw = $"{{\"player_uid\":\"{identity}\",\"season\":\"2025/26\",\"Goals\":{value},\"minutes_played\":{minutes}}}";

                    var row = connection.CreateCommand();
                    row.CommandText = """
                        INSERT INTO ExternalStatRows(
                            ImportId,RowIndex,PlayerIdentity,PlayerSourceId,PlayerName,Club,Season,Minutes,RawJson)
                        VALUES($import,$index,$identity,$source,$name,'Test FC','FM View Export',$minutes,$raw);
                        """;
                    row.Parameters.AddWithValue("$import", importId);
                    row.Parameters.AddWithValue("$index", index);
                    row.Parameters.AddWithValue("$identity", identity);
                    row.Parameters.AddWithValue("$source", sourceId);
                    row.Parameters.AddWithValue("$name", "Cohort Player " + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture));
                    row.Parameters.AddWithValue("$minutes", minutes);
                    row.Parameters.AddWithValue("$raw", raw);
                    await row.ExecuteNonQueryAsync();
                }
            }

            // There is intentionally no FMSave snapshot. The metric catalogue already
            // exposes FM View fields, so both query paths must be able to consume that source.
            var standard = await database.SearchSeasonMetricAsync("Goals", null, null, limit: 5000);
            Assert.AreEqual(1005, standard.Count);
            Assert.AreEqual(1005d, standard[0].Value, 0.0001);
            Assert.AreEqual("FMVIEW:player-1004", standard[0].SourceId);

            var per90 = await database.SearchSeasonMetricPer90Async(
                "Goals", null, null, minimumMinutes: 450, limit: 5000);
            Assert.AreEqual(1004, per90.Count, "The per-90 query must enforce its minimum-minutes threshold.");
            Assert.AreEqual(100.5d, per90[0].Value, 0.0001);
            Assert.AreEqual("FMVIEW:player-1004", per90[0].SourceId);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
