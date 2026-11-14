using System.IO;
using FmAtlas.Data;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FmAtlas.Tests;

[TestClass]
public sealed class ScoutDatabaseConcurrencyIntegrationTests
{
    [TestMethod]
    public async Task WriteWaitsForConcurrentWriterInsteadOfFailingImmediately()
    {
        var folder = Path.Combine(Path.GetTempPath(), "FmAtlas.SqliteConcurrency-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databasePath = Path.Combine(folder, "scouting.db");

        try
        {
            var database = new ScoutDatabase(databasePath, pooling: false);
            await database.InitialiseAsync();

            await using var blocker = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString());
            await blocker.OpenAsync();
            await using var transaction = await blocker.BeginTransactionAsync();

            var holdWriteLock = blocker.CreateCommand();
            holdWriteLock.Transaction = (SqliteTransaction)transaction;
            holdWriteLock.CommandText = """
                INSERT INTO ShortlistGroups(ShortlistName, CreatedUtc)
                VALUES('Lock holder', '2026-11-14T00:00:00Z');
                """;
            await holdWriteLock.ExecuteNonQueryAsync();

            // Atlas must tolerate a brief overlapping writer during imports/workspace activity.
            var waitingWrite = database.CreateShortlistAsync("After lock");
            await Task.Delay(250);
            Assert.IsFalse(waitingWrite.IsCompleted,
                "The second writer should wait while the first transaction owns the SQLite write lock.");

            await transaction.CommitAsync();
            await waitingWrite.WaitAsync(TimeSpan.FromSeconds(5));

            var names = await database.GetShortlistNamesAsync();
            CollectionAssert.Contains(names.ToArray(), "After lock");
            CollectionAssert.Contains(names.ToArray(), "Lock holder");
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
