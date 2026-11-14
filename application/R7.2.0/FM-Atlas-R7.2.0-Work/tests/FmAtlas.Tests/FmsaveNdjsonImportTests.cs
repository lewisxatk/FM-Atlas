using System.Text.Json;
using FmAtlas.Data;
using FmAtlas.Models;
using FmAtlas.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FmAtlas.Tests;

public sealed class FmsaveNdjsonImportTests
{
    [Fact]
    public async Task ValidBundlePublishesOneCareerSnapshot()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        var bundle = CreateBundle(temp.Path, "valid", playerManifestCount: 1, includeContractRow: true);

        var result = await database.ImportFmsaveSnapshotAsync(bundle);

        Assert.Equal(1, result.PlayersImported);
        Assert.Equal(1, result.ExtraRowsImported);
        Assert.Equal(0, result.StaffImported);
        Assert.Equal(0, result.ClubsImported);
        Assert.Equal(1, await ScalarIntAsync(databasePath, "SELECT COUNT(*) FROM FmsaveImportRuns;"));
        Assert.Equal("Fixture Player", await ScalarStringAsync(databasePath, "SELECT Name FROM Players WHERE SourceId='FMSAVE:FM27:test-db:1001';"));
        Assert.Equal(1, await ScalarIntAsync(databasePath, "SELECT COUNT(*) FROM FmsaveExtraTables WHERE TableName='contracts';"));
    }

    [Fact]
    public async Task PlayerManifestCountMismatchRollsBackNewSnapshot()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        var bundle = CreateBundle(temp.Path, "truncated", playerManifestCount: 2, includeContractRow: false);

        await Assert.ThrowsAsync<InvalidDataException>(() => database.ImportFmsaveSnapshotAsync(bundle));

        Assert.Equal(0, await ScalarIntAsync(databasePath, "SELECT COUNT(*) FROM FmsaveImportRuns;"));
        Assert.Equal(0, await ScalarIntAsync(databasePath, "SELECT COUNT(*) FROM Players;"));
        Assert.Equal(0, await ScalarIntAsync(databasePath, "SELECT COUNT(*) FROM FmsavePlayerSnapshots;"));
    }

    [Fact]
    public async Task MalformedExtraRowRollsBackPlayerAndImportRun()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        var bundle = CreateBundle(temp.Path, "malformed", playerManifestCount: 1, includeContractRow: true, malformedContractRow: true);

        await Assert.ThrowsAsync<InvalidDataException>(() => database.ImportFmsaveSnapshotAsync(bundle));

        Assert.Equal(0, await ScalarIntAsync(databasePath, "SELECT COUNT(*) FROM FmsaveImportRuns;"));
        Assert.Equal(0, await ScalarIntAsync(databasePath, "SELECT COUNT(*) FROM Players;"));
    }

    [Fact]
    public async Task CancellationImmediatelyBeforeCommitRollsBackNewSnapshot()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        var bundle = CreateBundle(temp.Path, "cancelled", playerManifestCount: 1, includeContractRow: true);
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress<string>(message =>
        {
            if (message.StartsWith("Validating and committing", StringComparison.OrdinalIgnoreCase))
                cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            database.ImportFmsaveSnapshotAsync(bundle, cancellation.Token, progress));

        Assert.Equal(0, await ScalarIntAsync(databasePath, "SELECT COUNT(*) FROM FmsaveImportRuns;"));
        Assert.Equal(0, await ScalarIntAsync(databasePath, "SELECT COUNT(*) FROM Players;"));
        Assert.Equal(0, await ScalarIntAsync(databasePath, "SELECT COUNT(*) FROM FmsaveExtraTables;"));
    }

    [Fact]
    public async Task SkippedStaffRowsPersistReconciliationWarnings()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        var bundle = CreateBundle(temp.Path, "staff-warning", playerManifestCount: 1, includeContractRow: false, includeInvalidStaffRow: true);

        var result = await database.ImportFmsaveSnapshotAsync(bundle);
        var savedWarnings = await ScalarStringAsync(databasePath, "SELECT WarningsJson FROM FmsaveImportRuns LIMIT 1;");

        Assert.Contains(result.Warnings, x => x.Contains("Staff rows skipped", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Staff rows skipped", savedWarnings ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, result.PlayersImported);
        Assert.Equal(0, result.StaffImported);
    }

    [Fact]
    public async Task FailedSecondImportPreservesPreviouslyCommittedSnapshot()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        var validBundle = CreateBundle(temp.Path, "first", playerManifestCount: 1, includeContractRow: false);
        await database.ImportFmsaveSnapshotAsync(validBundle);

        var invalidBundle = CreateBundle(temp.Path, "second", playerManifestCount: 3, includeContractRow: false);
        await Assert.ThrowsAsync<InvalidDataException>(() => database.ImportFmsaveSnapshotAsync(invalidBundle));

        Assert.Equal(1, await ScalarIntAsync(databasePath, "SELECT COUNT(*) FROM FmsaveImportRuns;"));
        Assert.Equal(1, await ScalarIntAsync(databasePath, "SELECT COUNT(*) FROM FmsavePlayerSnapshots;"));
        Assert.Equal("Fixture Player", await ScalarStringAsync(databasePath, "SELECT Name FROM Players WHERE SourceId='FMSAVE:FM27:test-db:1001';"));
    }

    [Fact]
    public async Task IndexedStatSearchAppliesAllAnyAndPer90ConditionsBeforeReturningRows()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        var bundle = CreateBundle(temp.Path, "indexed-stats", playerManifestCount: 1, includeContractRow: false, includeSeasonStatsRow: true);
        await database.ImportFmsaveSnapshotAsync(bundle);

        var allMatch = await database.SearchSeasonStatCriteriaAsync(
            new[]
            {
                new StatFilterCriterion("goals", "Goals", 10, null),
                new StatFilterCriterion("assists", "Assists", 8, null)
            }, per90: false, minimumMinutes: 450, limit: 500, matchAll: true);

        var anyMatch = await database.SearchSeasonStatCriteriaAsync(
            new[]
            {
                new StatFilterCriterion("goals", "Goals", 11, null),
                new StatFilterCriterion("assists", "Assists", 8, null)
            }, per90: false, minimumMinutes: 450, limit: 500, matchAll: false);

        var noMatch = await database.SearchSeasonStatCriteriaAsync(
            new[]
            {
                new StatFilterCriterion("goals", "Goals", 11, null),
                new StatFilterCriterion("assists", "Assists", 9, null)
            }, per90: false, minimumMinutes: 450, limit: 500, matchAll: false);

        var per90Match = await database.SearchSeasonStatCriteriaAsync(
            new[]
            {
                new StatFilterCriterion("goals", "Goals per 90", 0.99, null),
                new StatFilterCriterion("assists", "Assists per 90", 0.79, null)
            }, per90: true, minimumMinutes: 600, limit: 500, matchAll: true);

        Assert.Single(allMatch);
        Assert.Equal("Fixture Player", allMatch[0].Name);
        Assert.Contains("Goals: 10", allMatch[0].ValuesSummary);
        Assert.Single(anyMatch);
        Assert.Empty(noMatch);
        Assert.Single(per90Match);
        Assert.Contains("Goals per 90: 1", per90Match[0].ValuesSummary);
        Assert.Contains("Assists per 90: 0.8", per90Match[0].ValuesSummary);
    }

    [Fact]
    public async Task RoleFitSearchAppliesRecruitmentFiltersAndCoverageBeforeRanking()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        var bundle = CreateBundle(temp.Path, "role-fit", playerManifestCount: 1, includeContractRow: false);
        await database.ImportFmsaveSnapshotAsync(bundle);

        var profile = new RoleFitProfile(
            "Fixture Forward", "In Possession", "Attacker", "Regression profile.",
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Finishing"] = 1,
                ["Pace"] = 1
            });

        var matching = await database.SearchRoleFitCandidatesAsync(
            profile, 50000, new RoleFitSearchFilters(MinimumCoverage: 1, ResultLimit: 25));
        Assert.Single(matching);
        Assert.Equal(15.5, matching[0].Score, 2);
        Assert.Equal(2, matching[0].AttributesAvailable);
        Assert.Equal(2, matching[0].AttributesExpected);
        Assert.Equal(1d, matching[0].Coverage);

        var ageFiltered = await database.SearchRoleFitCandidatesAsync(
            profile, 50000, new RoleFitSearchFilters(MinimumAge: 21, ResultLimit: 25));
        Assert.Empty(ageFiltered);

        var caFiltered = await database.SearchRoleFitCandidatesAsync(
            profile, 50000, new RoleFitSearchFilters(MinimumCurrentAbility: 121, ResultLimit: 25));
        Assert.Empty(caFiltered);

        var paFiltered = await database.SearchRoleFitCandidatesAsync(
            profile, 50000, new RoleFitSearchFilters(MinimumPotentialAbility: 151, ResultLimit: 25));
        Assert.Empty(paFiltered);

        var positionMatch = await database.SearchRoleFitCandidatesAsync(
            profile, 50000, new RoleFitSearchFilters(PositionContains: "ST", MinimumCoverage: 1, ResultLimit: 25));
        Assert.Single(positionMatch);
        var positionMismatch = await database.SearchRoleFitCandidatesAsync(
            profile, 50000, new RoleFitSearchFilters(PositionContains: "GK", MinimumCoverage: 1, ResultLimit: 25));
        Assert.Empty(positionMismatch);

        var incompleteProfile = new RoleFitProfile(
            "Incomplete", "In Possession", "Attacker", "Regression profile.",
            new Dictionary<string, double> { ["Finishing"] = 1, ["Pace"] = 1, ["Crossing"] = 1, ["Heading"] = 1 });
        var strictCoverage = await database.SearchRoleFitCandidatesAsync(
            incompleteProfile, 50000, new RoleFitSearchFilters(MinimumCoverage: 0.75, ResultLimit: 25));
        Assert.Empty(strictCoverage);
    }

    [Fact]
    public void RoleFitScoringExcludesMissingAndOutOfRangeAttributesAndReportsCoverage()
    {
        var profile = new RoleFitProfile(
            "Weighted Test", "Both", "Test", "Regression profile.",
            new Dictionary<string, double>
            {
                ["Finishing"] = 2,
                ["Pace"] = 1,
                ["Crossing"] = 1
            });
        var score = RoleFitService.CalculateScore(profile, new Dictionary<string, double>
        {
            ["finishing"] = 18,
            ["Pace"] = 12,
            ["Crossing"] = 0,
            ["Unknown"] = 19
        });

        Assert.NotNull(score);
        Assert.Equal(16, score!.Score, 2);
        Assert.Equal(2, score.AttributesAvailable);
        Assert.Equal(3, score.AttributesExpected);
        Assert.Equal(2d / 3d, score.Coverage, 5);
        Assert.Null(RoleFitService.CalculateScore(profile, new Dictionary<string, double>
        {
            ["finishing"] = 18
        }, minimumCoverage: 0.8));
    }


    [Fact]
    public void RoleFitBreakdownExplainsWeightedScoreAndNormalizesAttributeNames()
    {
        var profile = new RoleFitProfile(
            "Evidence Test", "Both", "Test", "Regression profile.",
            new Dictionary<string, double>
            {
                ["Finishing"] = 2,
                ["finish_ing"] = 1,
                ["Pace"] = 1,
                ["Crossing"] = 4
            });
        var score = RoleFitService.CalculateScore(profile, new Dictionary<string, double>
        {
            ["finishing"] = 18,
            ["Pace"] = 12,
            ["Crossing"] = double.NaN,
            ["Unknown"] = 20
        }, minimumCoverage: 0.5);

        Assert.NotNull(score);
        Assert.Equal(16.5, score!.Score, 2);
        Assert.Equal(2, score.AttributesAvailable);
        Assert.Equal(3, score.AttributesExpected);
        Assert.Equal(2d / 3d, score.Coverage, 5);
        Assert.Equal(2, score.Contributions.Count);
        Assert.Equal("Finishing", score.Contributions[0].Attribute);
        Assert.Equal(3, score.Contributions[0].Weight);
        Assert.Equal(54, score.Contributions[0].WeightedPoints);
        Assert.Equal(1d, score.Contributions.Sum(x => x.ShareOfTotal), 5);
    }

    [Fact]
    public async Task RoleFitSearchCombinesEquivalentNormalizedWeightKeys()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        var bundle = CreateBundle(temp.Path, "role-fit-duplicate-keys", playerManifestCount: 1, includeContractRow: false);
        await database.ImportFmsaveSnapshotAsync(bundle);

        var profile = new RoleFitProfile(
            "Duplicate key test", "In Possession", "Attacker", "Regression profile.",
            new Dictionary<string, double>
            {
                ["Finishing"] = 1,
                ["finish_ing"] = 1,
                ["Pace"] = 1
            });

        var matching = await database.SearchRoleFitCandidatesAsync(
            profile, 50000, new RoleFitSearchFilters(MinimumCoverage: 1, ResultLimit: 25));

        Assert.Single(matching);
        Assert.Equal(15.6667, matching[0].Score, 3);
        Assert.Equal(2, matching[0].AttributesAvailable);
        Assert.Equal(2, matching[0].AttributesExpected);
        Assert.Equal(2, matching[0].Contributions!.Count);
    }


    [Fact]
    public async Task SimilarPlayerDiscoveryRanksSharedAttributesAndReportsCoverage()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        var bundle = CreateBundle(temp.Path, "similar-player-anchor", playerManifestCount: 1, includeContractRow: false);
        await database.ImportFmsaveSnapshotAsync(bundle);

        await using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString()))
        {
            await db.OpenAsync();
            foreach (var candidate in new[]
            {
                (Id: "LOCAL:similar:close", Name: "Closer Player", Finishing: 15d, Pace: 14d),
                (Id: "LOCAL:similar:far", Name: "Distant Player", Finishing: 1d, Pace: 2d)
            })
            {
                var player = db.CreateCommand();
                player.CommandText = "INSERT INTO Players(SourceId,Name,Age,Club,Position,CurrentAbility,PotentialAbility,DataOrigin,UpdatedUtc) VALUES($id,$name,21,'Test FC','ST',110,130,'Test fixture','2026-11-14');";
                player.Parameters.AddWithValue("$id", candidate.Id);
                player.Parameters.AddWithValue("$name", candidate.Name);
                await player.ExecuteNonQueryAsync();
                foreach (var (attribute, value) in new[] { ("finishing", candidate.Finishing), ("pace", candidate.Pace) })
                {
                    var fact = db.CreateCommand();
                    fact.CommandText = "INSERT INTO PlayerAttributes(SourceId,AttributeName,Category,AttributeValue,UpdatedUtc) VALUES($id,$attribute,'attributes',$value,'2026-11-14');";
                    fact.Parameters.AddWithValue("$id", candidate.Id);
                    fact.Parameters.AddWithValue("$attribute", attribute);
                    fact.Parameters.AddWithValue("$value", value);
                    await fact.ExecuteNonQueryAsync();
                }
            }
        }

        var matches = await database.FindSimilarPlayersAsync(
            "FMSAVE:FM27:test-db:1001", limit: 10, minimumCommonAttributes: 2, minimumAnchorCoverage: 1);

        Assert.Equal(2, matches.Count);
        Assert.Equal("LOCAL:similar:close", matches[0].Player.SourceId);
        Assert.True(matches[0].SimilarityScore > matches[1].SimilarityScore);
        Assert.Equal(2, matches[0].AttributesCompared);
        Assert.Equal(2, matches[0].ReferenceAttributes);
        Assert.Equal(1d, matches[0].AttributeCoverage);
    }

    [Fact]
    public async Task IndexedStatSearchReturnsCorrectAllAnyMatchesAcrossFortyThousandPlayerPeriods()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        var bundle = CreateBundle(temp.Path, "large-stat-fixture", playerManifestCount: 1, includeContractRow: false);
        await database.ImportFmsaveSnapshotAsync(bundle);
        var snapshotId = await ScalarIntAsync(databasePath, "SELECT MAX(SnapshotId) FROM FmsaveImportRuns;");

        var seedTimer = System.Diagnostics.Stopwatch.StartNew();
        await using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString()))
        {
            await db.OpenAsync();
            await using var transaction = await db.BeginTransactionAsync();
            var insertPlayer = db.CreateCommand();
            insertPlayer.Transaction = (SqliteTransaction)transaction;
            insertPlayer.CommandText = "INSERT INTO Players(SourceId,Name,Age,Club,Position,CurrentAbility,PotentialAbility,DataOrigin,UpdatedUtc) VALUES($source,$name,20,'Scale FC','ST',100,140,'Synthetic scale fixture','2026-11-14');";
            insertPlayer.Parameters.Add("$source", SqliteType.Text);
            insertPlayer.Parameters.Add("$name", SqliteType.Text);
            insertPlayer.Prepare();

            var insertSnapshot = db.CreateCommand();
            insertSnapshot.Transaction = (SqliteTransaction)transaction;
            insertSnapshot.CommandText = "INSERT INTO FmsavePlayerSnapshots(SnapshotId,PlayerUid,PlayerSourceId,RawJson) VALUES($snapshot,$uid,$source,'{}');";
            insertSnapshot.Parameters.Add("$snapshot", SqliteType.Integer);
            insertSnapshot.Parameters.Add("$uid", SqliteType.Text);
            insertSnapshot.Parameters.Add("$source", SqliteType.Text);
            insertSnapshot.Prepare();

            var insertFact = db.CreateCommand();
            insertFact.Transaction = (SqliteTransaction)transaction;
            insertFact.CommandText = "INSERT INTO FmsaveStatFacts(SnapshotId,TableName,RowIndex,PlayerUid,PeriodKey,Minutes,FieldKey,NumericValue) VALUES($snapshot,'player_season_stats',$row,$uid,'2026/27',$minutes,$field,$value);";
            insertFact.Parameters.Add("$snapshot", SqliteType.Integer);
            insertFact.Parameters.Add("$row", SqliteType.Integer);
            insertFact.Parameters.Add("$uid", SqliteType.Text);
            insertFact.Parameters.Add("$minutes", SqliteType.Real);
            insertFact.Parameters.Add("$field", SqliteType.Text);
            insertFact.Parameters.Add("$value", SqliteType.Real);
            insertFact.Prepare();

            const int playerCount = 40000;
            for (var i = 0; i < playerCount; i++)
            {
                var uid = "scale-uid-" + i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
                var source = "FMSAVE:FM27:scale:" + i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
                var goals = i % 40;
                var assists = i % 10;
                var minutes = 600 + (i % 600);

                insertPlayer.Parameters["$source"].Value = source;
                insertPlayer.Parameters["$name"].Value = "Scale Player " + i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
                await insertPlayer.ExecuteNonQueryAsync();

                insertSnapshot.Parameters["$snapshot"].Value = snapshotId;
                insertSnapshot.Parameters["$uid"].Value = uid;
                insertSnapshot.Parameters["$source"].Value = source;
                await insertSnapshot.ExecuteNonQueryAsync();

                foreach (var metric in new[] { ("goals", (double)goals), ("assists", (double)assists) })
                {
                    insertFact.Parameters["$snapshot"].Value = snapshotId;
                    insertFact.Parameters["$row"].Value = i;
                    insertFact.Parameters["$uid"].Value = uid;
                    insertFact.Parameters["$minutes"].Value = minutes;
                    insertFact.Parameters["$field"].Value = metric.Item1;
                    insertFact.Parameters["$value"].Value = metric.Item2;
                    await insertFact.ExecuteNonQueryAsync();
                }
            }
            await transaction.CommitAsync();
        }
        seedTimer.Stop();

        var timer = System.Diagnostics.Stopwatch.StartNew();
        var allMatches = await database.SearchSeasonStatCriteriaAsync(
            new[] { new StatFilterCriterion("goals", "Goals", 31, null), new StatFilterCriterion("assists", "Assists", 6, null) },
            per90: false, minimumMinutes: 450, limit: 5000, matchAll: true);
        var anyMatches = await database.SearchSeasonStatCriteriaAsync(
            new[] { new StatFilterCriterion("goals", "Goals", 38, null), new StatFilterCriterion("assists", "Assists", 9, null) },
            per90: false, minimumMinutes: 450, limit: 5000, matchAll: false);
        timer.Stop();

        Assert.Equal(4000, allMatches.Count);
        Assert.Equal(5000, anyMatches.Count);
        Assert.Contains(allMatches, x => x.SourceId == "FMSAVE:FM27:scale:00036");
        Assert.DoesNotContain(allMatches, x => x.SourceId == "FMSAVE:FM27:scale:00030");
        Assert.Contains(anyMatches, x => x.SourceId == "FMSAVE:FM27:scale:00009");
        Assert.Contains(anyMatches, x => x.SourceId == "FMSAVE:FM27:scale:00038");
        Assert.True(seedTimer.Elapsed < TimeSpan.FromSeconds(90), $"Seeding 40,000 player periods took {seedTimer.Elapsed.TotalSeconds:0.00}s.");
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(45), $"Indexed 40,000-player ALL/ANY searches took {timer.Elapsed.TotalSeconds:0.00}s.");
    }


    [Fact]
    public async Task PerformanceValueAnalysisUsesSourceMetricMinutesAndValuePercentiles()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();
        var bundle = CreateBundle(temp.Path, "performance-value", playerManifestCount: 1, includeContractRow: false, includeSeasonStatsRow: true);
        await database.ImportFmsaveSnapshotAsync(bundle);

        var results = await database.GetPerformanceValueCandidatesAsync("goals", per90: true, minimumMinutes: 450);

        Assert.Single(results);
        Assert.Equal("FMSAVE:FM27:test-db:1001", results[0].Player.SourceId);
        Assert.Equal(1d, results[0].PerformanceValue, 5);
        Assert.Equal(900d, results[0].TotalMinutes);
        Assert.Equal(1, results[0].SampleSize);
        Assert.Equal(50d, results[0].PerformancePercentile);
        Assert.Equal(50d, results[0].ValuePercentile);
        Assert.Null(results[0].PositionPerformancePercentile);
        Assert.Equal(1, results[0].PositionCohortSize);
        Assert.Null(results[0].LeaguePerformancePercentile);
        Assert.Equal(1, results[0].LeagueCohortSize);
        Assert.Equal(0d, results[0].ValueGap);
        Assert.Equal(100d, results[0].DataCoverage);
    }


    [Fact]
    public void RoleFitProfileStoreAtomicallySavesReplacesLoadsAndDeletesCustomTemplates()
    {
        using var temp = new TemporaryFolder();
        var path = Path.Combine(temp.Path, "role-templates.json");
        var first = new RoleFitProfile(
            "Barnstorming Wingback", "In Possession", "Defender", "High-energy wide progression.",
            new Dictionary<string, double> { ["Pace"] = 2, ["Stamina"] = 1.5, ["Crossing"] = 1 });
        RoleFitProfileStore.Save(first, path);

        var loaded = Assert.Single(RoleFitProfileStore.LoadAll(path));
        Assert.True(loaded.IsCustom);
        Assert.Equal("Barnstorming Wingback", loaded.Name);
        Assert.Equal(2d, loaded.Weights["Pace"]);

        var replacement = first with
        {
            Description = "Updated wide progression template.",
            Weights = new Dictionary<string, double> { ["Pace"] = 3, ["Dribbling"] = 2 }
        };
        RoleFitProfileStore.Save(replacement, path);

        var replaced = Assert.Single(RoleFitProfileStore.LoadAll(path));
        Assert.Equal("Updated wide progression template.", replaced.Description);
        Assert.Equal(3d, replaced.Weights["Pace"]);
        Assert.True(RoleFitProfileStore.Delete("Barnstorming Wingback", path));
        Assert.Empty(RoleFitProfileStore.LoadAll(path));
        Assert.False(RoleFitProfileStore.Delete("Barnstorming Wingback", path));
    }

    [Fact]
    public void RoleFitProfileStoreRejectsInvalidWeightsAndIncompleteTemplates()
    {
        using var temp = new TemporaryFolder();
        var path = Path.Combine(temp.Path, "invalid-role-templates.json");
        var invalid = new RoleFitProfile(
            "Broken template", "Both", "Test", "Invalid weight.",
            new Dictionary<string, double> { ["Pace"] = 0, ["Finishing"] = 1 });
        Assert.Throws<ArgumentException>(() => RoleFitProfileStore.Save(invalid, path));
        Assert.False(File.Exists(path));
    }


    [Fact]
    public void RoleFitTemplateStoreRoundTripsCustomWeightsAndRecruitmentFilters()
    {
        using var temp = new TemporaryFolder();
        var path = Path.Combine(temp.Path, "role-fit-templates.json");
        var store = new RoleFitTemplateStore(path);
        var profile = new RoleFitProfile(
            "Advanced Pressing Forward", "Out of Possession", "Attacker", "Custom pressing forward.",
            new Dictionary<string, double>
            {
                ["Work Rate"] = 3,
                ["Stamina"] = 2,
                ["Finishing"] = 0.5
            });
        var filters = new RoleFitSearchFilters(
            MinimumAge: 17,
            MaximumAge: 24,
            MinimumCurrentAbility: 95,
            MinimumPotentialAbility: 135,
            PositionContains: "ST",
            MinimumCoverage: 0.8,
            ResultLimit: 125);

        store.Save(new RoleFitSavedTemplate("U24 Pressing 9", profile, filters));

        var loaded = Assert.Single(store.LoadAll());
        Assert.Equal("U24 Pressing 9", loaded.Name);
        Assert.Equal("Advanced Pressing Forward", loaded.Profile.Name);
        Assert.Equal(3, loaded.Profile.Weights["Work Rate"]);
        Assert.Equal(0.5, loaded.Profile.Weights["Finishing"]);
        Assert.Equal(17, loaded.Filters.MinimumAge);
        Assert.Equal(24, loaded.Filters.MaximumAge);
        Assert.Equal(95, loaded.Filters.MinimumCurrentAbility);
        Assert.Equal(135, loaded.Filters.MinimumPotentialAbility);
        Assert.Equal("ST", loaded.Filters.PositionContains);
        Assert.Equal(0.8, loaded.Filters.MinimumCoverage);
        Assert.Equal(125, loaded.Filters.ResultLimit);

        store.Save(new RoleFitSavedTemplate(
            "U24 Pressing 9",
            profile with { Weights = new Dictionary<string, double> { ["Work Rate"] = 4 } },
            filters with { MinimumCoverage = 0.9 }));
        var replaced = Assert.Single(store.LoadAll());
        Assert.Equal(4, replaced.Profile.Weights["Work Rate"]);
        Assert.Equal(0.9, replaced.Filters.MinimumCoverage);
        Assert.True(store.Delete("U24 Pressing 9"));
        Assert.Empty(store.LoadAll());
        Assert.False(store.Delete("U24 Pressing 9"));
    }

    [Fact]
    public void RoleFitTemplateStoreRejectsInvalidWeightsAndPreservesExistingTemplates()
    {
        using var temp = new TemporaryFolder();
        var store = new RoleFitTemplateStore(Path.Combine(temp.Path, "templates.json"));
        var profile = new RoleFitProfile(
            "Valid Role", "Both", "Any", "Valid base.",
            new Dictionary<string, double> { ["Finishing"] = 2 });
        store.Save(new RoleFitSavedTemplate("Valid Template", profile, new RoleFitSearchFilters()));

        var invalid = profile with
        {
            Weights = new Dictionary<string, double> { ["Finishing"] = double.NaN }
        };
        Assert.Throws<ArgumentException>(() =>
            store.Save(new RoleFitSavedTemplate("Broken Template", invalid, new RoleFitSearchFilters())));
        Assert.Single(store.LoadAll());
        Assert.Equal("Valid Template", store.LoadAll()[0].Name);
    }


    [Fact]
    public async Task PlayerDevelopmentHistoryUsesOnlyDistinctDatedSourceSnapshots()
    {
        using var temp = new TemporaryFolder();
        var databasePath = Path.Combine(temp.Path, "scouting.db");
        var database = new ScoutDatabase(databasePath);
        await database.InitialiseAsync();

        var first = CreateBundle(temp.Path, "history-first", playerManifestCount: 1, includeContractRow: false,
            currentAbility: 120, potentialAbility: 150, gameDate: "2026-11-14");
        var second = CreateBundle(temp.Path, "history-second", playerManifestCount: 1, includeContractRow: false,
            currentAbility: 128, potentialAbility: 158, gameDate: "2027-01-02");
        var duplicateDate = CreateBundle(temp.Path, "history-duplicate-date", playerManifestCount: 1, includeContractRow: false,
            currentAbility: 130, potentialAbility: 160, gameDate: "2027-01-02");

        await database.ImportFmsaveSnapshotAsync(first);
        await database.ImportFmsaveSnapshotAsync(second);
        await database.ImportFmsaveSnapshotAsync(duplicateDate);

        var history = await database.GetPlayerDevelopmentHistoryAsync("FMSAVE:FM27:test-db:1001");

        Assert.Equal(3, history.Count);
        Assert.Equal(120d, history[0].CurrentAbility);
        Assert.Equal(150d, history[0].PotentialAbility);
        Assert.Equal(new DateTime(2026, 11, 14), history[0].GameDate);
        Assert.Equal(130d, history[2].CurrentAbility);
        Assert.Equal(160d, history[2].PotentialAbility);
        Assert.Equal(new DateTime(2027, 1, 2), history[2].GameDate);
        Assert.Equal("history-duplicate-date.fm", history[2].SourceFileName);
    }

    private static string CreateBundle(
        string root,
        string name,
        int playerManifestCount,
        bool includeContractRow,
        bool malformedContractRow = false,
        bool includeInvalidStaffRow = false,
        bool includeSeasonStatsRow = false,
        int currentAbility = 120,
        int potentialAbility = 150,
        string gameDate = "2026-11-14")
    {
        var folder = Path.Combine(root, name);
        Directory.CreateDirectory(folder);

        var counts = new Dictionary<string, int>
        {
            ["players"] = playerManifestCount,
            ["staff"] = includeInvalidStaffRow ? 1 : 0,
            ["clubs"] = 0,
            ["contracts"] = includeContractRow ? 1 : 0,
            ["player_season_stats"] = includeSeasonStatsRow ? 1 : 0
        };
        var manifest = new
        {
            schema = "fmatlas.fmsave.snapshot.ndjson.v1",
            created_utc = DateTime.UtcNow.ToString("O"),
            source_file_name = name + ".fm",
            source_file_size = 128,
            fmsave_version = "test-fixture",
            save_info = new { game = "FM27", build = "fixture", known_build = true, db_version = "test-db", game_date = gameDate },
            validation = new { readers = Array.Empty<object>() },
            counts,
            warnings = Array.Empty<string>()
        };
        File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(manifest));

        var player = new
        {
            uid = "player-uid-1001",
            unique_id = 1001,
            atlas_source_id = "FMSAVE:FM27:test-db:1001",
            name = "Fixture Player",
            age = 20,
            club_uid = "club-10",
            club_name = "Fixture FC",
            nationality = "England",
            league = "Test League",
            position = "ST",
            current_ability = currentAbility,
            potential_ability = potentialAbility,
            value = 1250000,
            preferred_foot = "Right",
            left_foot = 10,
            right_foot = 18,
            attributes = new { finishing = 16, pace = 15 },
            personality = new { professionalism = 17 },
            contract = new { wage = 10000, contract_end = "2028-06-30" }
        };
        File.WriteAllText(Path.Combine(folder, "players.ndjson"), JsonSerializer.Serialize(player) + Environment.NewLine);

        if (includeInvalidStaffRow)
            File.WriteAllText(Path.Combine(folder, "staff.ndjson"), JsonSerializer.Serialize(new { name = "Missing Staff UID" }) + Environment.NewLine);

        if (includeContractRow)
        {
            var contractPath = Path.Combine(folder, "contracts.ndjson");
            File.WriteAllText(contractPath, malformedContractRow
                ? "{ this is not valid json }" + Environment.NewLine
                : JsonSerializer.Serialize(new { player_uid = "player-uid-1001", wage = 10000 }) + Environment.NewLine);
        }

        if (includeSeasonStatsRow)
        {
            var stats = new
            {
                player_uid = "player-uid-1001",
                season = "2026/27",
                minutes_played = 900,
                goals = 10,
                assists = 8
            };
            File.WriteAllText(Path.Combine(folder, "player_season_stats.ndjson"), JsonSerializer.Serialize(stats) + Environment.NewLine);
        }

        return folder;
    }

    private static async Task<int> ScalarIntAsync(string databasePath, string sql)
    {
        await using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        await db.OpenAsync();
        var command = db.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<string?> ScalarStringAsync(string databasePath, string sql)
    {
        await using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        await db.OpenAsync();
        var command = db.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class TemporaryFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FmAtlasTests", Guid.NewGuid().ToString("N"));

        public TemporaryFolder() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
            catch { /* SQLite WAL handles may still be releasing during test teardown. */ }
        }
    }
}
