using Xunit;

// These integration tests create and remove SQLite databases. Microsoft.Data.Sqlite's
// ClearAllPools() is process-wide, so running collections concurrently can close a
// connection owned by another test and make temporary-directory cleanup race.
// Keep the integration suite deterministic; the full suite completes in under a minute.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
