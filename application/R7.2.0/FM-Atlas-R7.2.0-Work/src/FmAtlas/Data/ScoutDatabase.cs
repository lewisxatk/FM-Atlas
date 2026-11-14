using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Runtime.CompilerServices;
using FmAtlas.Models;
using FmAtlas.Services;

namespace FmAtlas.Data;

public sealed record LiveClubSummary(long ClubUid, string Name, string ValidationStatus, int LinkedPlayers);
public sealed record WorkspaceMetrics(int LivePlayers, int LiveClubs, int LiveStaff, int SavePlayers, int SaveClubs, int SaveStaff, int LocalAttributeFacts, int LiveFacts, string? LastSaveImportUtc, string? LastLiveImportUtc, string? LastSaveFileName, string? SaveGame, string? SaveBuild, bool? SaveBuildKnown, string? SaveGameDate, string? SaveValidationJson, string? CurrentManager, string? CurrentManagerClub);
public sealed record AtlasStaffRecord(string SourceId, string Name, string? Club, string? Job, int? Age, string? Nation, string ValidationStatus);
public sealed record AtlasClubRecord(string SourceId, string Name, string? Nation, int PlayerCount, string DataSource);
public sealed record AtlasHistoryRecord(long Id, string SourceId, string PlayerName, string FieldName, string? OldValue, string? NewValue, string ChangedUtc, string ChangeSource);
public sealed record AtlasFactRecord(string Group, string Field, string? Value);
public sealed record AtlasEditableAttribute(string Name, string Category, double Value);
public sealed record AnalyticsMetricResult(string SourceId, string Name, string? Club, string Season, double Value);
public sealed record FmsaveImportResult(int SnapshotId, int PlayersImported, int StaffImported, int ClubsImported, int ExtraRowsImported, IReadOnlyDictionary<string,int> TableCounts, IReadOnlyList<string> Warnings);

public sealed class ScoutDatabase
{
    private readonly string _connectionString;
    // All mutating operations across ScoutDatabase instances share one writer gate.
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    public ScoutDatabase()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FmAtlas");
        Directory.CreateDirectory(folder);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(folder, "scouting.db"),
            DefaultTimeout = 30,
            ForeignKeys = true
        }.ToString();
    }

    public async Task InitialiseAsync()
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS Players (
              SourceId TEXT PRIMARY KEY, Name TEXT NOT NULL, Age INTEGER NULL,
              Club TEXT NULL, Nation TEXT NULL, League TEXT NULL, Position TEXT NULL,
              CurrentAbility INTEGER NULL, PotentialAbility INTEGER NULL, Value REAL NULL,
              HomeGrownClub INTEGER NULL, HomeGrownNation INTEGER NULL, PreferredFoot TEXT NULL,
              DataOrigin TEXT NOT NULL DEFAULT 'Unknown', UpdatedUtc TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS PlayerAttributes (
              SourceId TEXT NOT NULL, AttributeName TEXT NOT NULL, Category TEXT NOT NULL DEFAULT 'Other',
              AttributeValue REAL NOT NULL, UpdatedUtc TEXT NOT NULL,
              PRIMARY KEY(SourceId, AttributeName), FOREIGN KEY(SourceId) REFERENCES Players(SourceId) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_PlayerAttributes_NameValue ON PlayerAttributes(AttributeName, AttributeValue);
            CREATE TABLE IF NOT EXISTS EditHistory (
              Id INTEGER PRIMARY KEY AUTOINCREMENT, SourceId TEXT NOT NULL, PlayerName TEXT NOT NULL,
              FieldName TEXT NOT NULL, OldValue TEXT NULL, NewValue TEXT NULL, ChangedUtc TEXT NOT NULL,
              ChangeSource TEXT NOT NULL DEFAULT 'FM Atlas local editor'
            );
            CREATE INDEX IF NOT EXISTS IX_EditHistory_Player ON EditHistory(SourceId, ChangedUtc DESC);
            CREATE INDEX IF NOT EXISTS IX_Players_Name ON Players(Name);
            CREATE INDEX IF NOT EXISTS IX_Players_Age ON Players(Age);
            CREATE TABLE IF NOT EXISTS Shortlists (
              Id INTEGER PRIMARY KEY AUTOINCREMENT, SourceId TEXT NOT NULL,
              ShortlistName TEXT NOT NULL DEFAULT 'My Shortlist', Notes TEXT NULL,
              AddedUtc TEXT NOT NULL, UNIQUE(SourceId, ShortlistName)
            );
            CREATE INDEX IF NOT EXISTS IX_Shortlists_Name ON Shortlists(ShortlistName);
            CREATE TABLE IF NOT EXISTS ShortlistGroups (ShortlistName TEXT PRIMARY KEY, CreatedUtc TEXT NOT NULL);

            -- Live FM27 data is kept separate from the existing manually/CSV populated scouting tables.
            -- This prevents unverified live fields from silently overwriting trusted scouting data.
            CREATE TABLE IF NOT EXISTS LiveImportRuns (
              RunId INTEGER PRIMARY KEY AUTOINCREMENT, GameVersion TEXT NOT NULL, ModuleSha256 TEXT NOT NULL,
              StartedUtc TEXT NOT NULL, CompletedUtc TEXT NULL, Status TEXT NOT NULL,
              PlayersSeen INTEGER NOT NULL DEFAULT 0, PlayersImported INTEGER NOT NULL DEFAULT 0,
              ClubsSeen INTEGER NOT NULL DEFAULT 0, ClubsImported INTEGER NOT NULL DEFAULT 0,
              StaffSeen INTEGER NOT NULL DEFAULT 0, StaffImported INTEGER NOT NULL DEFAULT 0,
              ErrorSummary TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS LiveClubs (
              ClubUid INTEGER PRIMARY KEY, ClubName TEXT NOT NULL, ShortName TEXT NULL,
              ArrayIndex INTEGER NULL, ObjectAddress TEXT NULL, SourceRunId INTEGER NOT NULL,
              ValidationStatus TEXT NOT NULL DEFAULT 'Candidate', UpdatedUtc TEXT NOT NULL,
              FOREIGN KEY(SourceRunId) REFERENCES LiveImportRuns(RunId)
            );
            CREATE TABLE IF NOT EXISTS LiveTeams (
              TeamUid INTEGER PRIMARY KEY, TeamName TEXT NULL, ClubUid INTEGER NULL,
              ArrayIndex INTEGER NULL, ObjectAddress TEXT NULL, SourceRunId INTEGER NOT NULL,
              ValidationStatus TEXT NOT NULL DEFAULT 'Candidate', UpdatedUtc TEXT NOT NULL,
              FOREIGN KEY(ClubUid) REFERENCES LiveClubs(ClubUid),
              FOREIGN KEY(SourceRunId) REFERENCES LiveImportRuns(RunId)
            );
            CREATE TABLE IF NOT EXISTS LivePlayers (
              PlayerUid INTEGER PRIMARY KEY, PlayerName TEXT NOT NULL, TeamUid INTEGER NULL, ClubUid INTEGER NULL,
              ArrayIndex INTEGER NULL, ObjectAddress TEXT NULL, SourceRunId INTEGER NOT NULL,
              NameValidation TEXT NOT NULL DEFAULT 'Candidate', RelationshipValidation TEXT NOT NULL DEFAULT 'Candidate',
              UpdatedUtc TEXT NOT NULL, FOREIGN KEY(TeamUid) REFERENCES LiveTeams(TeamUid),
              FOREIGN KEY(ClubUid) REFERENCES LiveClubs(ClubUid), FOREIGN KEY(SourceRunId) REFERENCES LiveImportRuns(RunId)
            );
            CREATE TABLE IF NOT EXISTS LiveStaff (
              StaffUid INTEGER PRIMARY KEY, StaffName TEXT NOT NULL, ArrayIndex INTEGER NULL,
              ObjectAddress TEXT NULL, SourceRunId INTEGER NOT NULL, ValidationStatus TEXT NOT NULL DEFAULT 'Candidate',
              UpdatedUtc TEXT NOT NULL, FOREIGN KEY(SourceRunId) REFERENCES LiveImportRuns(RunId)
            );
            CREATE TABLE IF NOT EXISTS LiveFieldRegistry (
              EntityType TEXT NOT NULL, FieldKey TEXT NOT NULL, DisplayName TEXT NOT NULL,
              CandidateOffset TEXT NULL, DataType TEXT NOT NULL, ValidationStatus TEXT NOT NULL DEFAULT 'Unverified',
              Evidence TEXT NULL, LastVerifiedGameVersion TEXT NULL, LastVerifiedModuleSha256 TEXT NULL,
              PRIMARY KEY(EntityType, FieldKey)
            );
            CREATE TABLE IF NOT EXISTS LiveTeamLinks (
              TeamObjectAddress TEXT PRIMARY KEY, ClubUid INTEGER NULL, SourceRunId INTEGER NOT NULL,
              UpdatedUtc TEXT NOT NULL, FOREIGN KEY(ClubUid) REFERENCES LiveClubs(ClubUid),
              FOREIGN KEY(SourceRunId) REFERENCES LiveImportRuns(RunId)
            );
            CREATE INDEX IF NOT EXISTS IX_LivePlayers_Name ON LivePlayers(PlayerName);
            CREATE INDEX IF NOT EXISTS IX_LivePlayers_Club ON LivePlayers(ClubUid);
            CREATE INDEX IF NOT EXISTS IX_LiveTeams_Club ON LiveTeams(ClubUid);
            CREATE INDEX IF NOT EXISTS IX_LiveImportRuns_Started ON LiveImportRuns(StartedUtc DESC);

            -- Extensible read-only fact stores. Fields are populated only after their FM27 layouts are validated.
            CREATE TABLE IF NOT EXISTS LivePlayerFacts (
              PlayerUid INTEGER NOT NULL, FieldKey TEXT NOT NULL, NumericValue REAL NULL, TextValue TEXT NULL,
              ValidationStatus TEXT NOT NULL DEFAULT 'Unverified', SourceRunId INTEGER NOT NULL, UpdatedUtc TEXT NOT NULL,
              PRIMARY KEY(PlayerUid, FieldKey), FOREIGN KEY(PlayerUid) REFERENCES LivePlayers(PlayerUid),
              FOREIGN KEY(SourceRunId) REFERENCES LiveImportRuns(RunId)
            );
            CREATE TABLE IF NOT EXISTS LiveClubFacts (
              ClubUid INTEGER NOT NULL, FieldKey TEXT NOT NULL, NumericValue REAL NULL, TextValue TEXT NULL,
              ValidationStatus TEXT NOT NULL DEFAULT 'Unverified', SourceRunId INTEGER NOT NULL, UpdatedUtc TEXT NOT NULL,
              PRIMARY KEY(ClubUid, FieldKey), FOREIGN KEY(ClubUid) REFERENCES LiveClubs(ClubUid),
              FOREIGN KEY(SourceRunId) REFERENCES LiveImportRuns(RunId)
            );
            CREATE TABLE IF NOT EXISTS LiveStaffFacts (
              StaffUid INTEGER NOT NULL, FieldKey TEXT NOT NULL, NumericValue REAL NULL, TextValue TEXT NULL,
              ValidationStatus TEXT NOT NULL DEFAULT 'Unverified', SourceRunId INTEGER NOT NULL, UpdatedUtc TEXT NOT NULL,
              PRIMARY KEY(StaffUid, FieldKey), FOREIGN KEY(StaffUid) REFERENCES LiveStaff(StaffUid),
              FOREIGN KEY(SourceRunId) REFERENCES LiveImportRuns(RunId)
            );
            CREATE TABLE IF NOT EXISTS LiveAffiliations (
              ParentClubUid INTEGER NOT NULL, RelatedClubUid INTEGER NOT NULL, RelationshipType TEXT NOT NULL,
              ValidationStatus TEXT NOT NULL DEFAULT 'Unverified', SourceRunId INTEGER NOT NULL,
              PRIMARY KEY(ParentClubUid, RelatedClubUid, RelationshipType)
            );
            CREATE TABLE IF NOT EXISTS LiveCompetitions (
              CompetitionUid INTEGER PRIMARY KEY, CompetitionName TEXT NOT NULL, NationUid INTEGER NULL,
              ValidationStatus TEXT NOT NULL DEFAULT 'Unverified', SourceRunId INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS LiveNations (
              NationUid INTEGER PRIMARY KEY, NationName TEXT NOT NULL,
              ValidationStatus TEXT NOT NULL DEFAULT 'Unverified', SourceRunId INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS LivePlayerHistory (
              PlayerUid INTEGER NOT NULL, SeasonKey TEXT NOT NULL, ClubUid INTEGER NULL, CompetitionUid INTEGER NULL,
              FieldKey TEXT NOT NULL, NumericValue REAL NULL, TextValue TEXT NULL,
              ValidationStatus TEXT NOT NULL DEFAULT 'Unverified', SourceRunId INTEGER NOT NULL,
              PRIMARY KEY(PlayerUid, SeasonKey, FieldKey)
            );
            CREATE INDEX IF NOT EXISTS IX_LivePlayerFacts_Field ON LivePlayerFacts(FieldKey, NumericValue);
            CREATE INDEX IF NOT EXISTS IX_LiveClubFacts_Field ON LiveClubFacts(FieldKey, NumericValue);
            CREATE INDEX IF NOT EXISTS IX_LiveAffiliations_Parent ON LiveAffiliations(ParentClubUid);
            CREATE INDEX IF NOT EXISTS IX_LivePlayerHistory_Player ON LivePlayerHistory(PlayerUid, SeasonKey);
            """;
        await cmd.ExecuteNonQueryAsync();
        var snapshotSchema = db.CreateCommand();
        snapshotSchema.CommandText = """
            CREATE TABLE IF NOT EXISTS FmsaveImportRuns (
              SnapshotId INTEGER PRIMARY KEY AUTOINCREMENT, SourceFileName TEXT NOT NULL, SourceFileSize INTEGER NOT NULL,
              AdapterVersion TEXT NULL, CreatedUtc TEXT NOT NULL, PlayersImported INTEGER NOT NULL DEFAULT 0,
              StaffImported INTEGER NOT NULL DEFAULT 0, ClubsImported INTEGER NOT NULL DEFAULT 0,
              ExtraRowsImported INTEGER NOT NULL DEFAULT 0, WarningsJson TEXT NULL, TableCountsJson TEXT NULL,
              Game TEXT NULL, GameBuild TEXT NULL, KnownBuild INTEGER NULL, GameDate TEXT NULL, ValidationJson TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS FmsavePlayerSnapshots (
              SnapshotId INTEGER NOT NULL, PlayerUid TEXT NOT NULL, PlayerSourceId TEXT NULL, RawJson TEXT NOT NULL,
              PRIMARY KEY(SnapshotId, PlayerUid), FOREIGN KEY(SnapshotId) REFERENCES FmsaveImportRuns(SnapshotId)
            );
            CREATE TABLE IF NOT EXISTS FmsavePlayerFacts (
              SnapshotId INTEGER NOT NULL, PlayerSourceId TEXT NOT NULL, FactGroup TEXT NOT NULL,
              FieldKey TEXT NOT NULL, NumericValue REAL NULL, TextValue TEXT NULL,
              PRIMARY KEY(SnapshotId, PlayerSourceId, FactGroup, FieldKey),
              FOREIGN KEY(SnapshotId) REFERENCES FmsaveImportRuns(SnapshotId)
            );
            CREATE INDEX IF NOT EXISTS IX_FmsavePlayerFacts_Lookup ON FmsavePlayerFacts(PlayerSourceId, FactGroup);
            CREATE TABLE IF NOT EXISTS FmsaveStaffDirectory (
              SnapshotId INTEGER NOT NULL, StaffUid TEXT NOT NULL, Name TEXT NOT NULL, ClubName TEXT NULL,
              JobTitle TEXT NULL, Age INTEGER NULL, Nation TEXT NULL, RawJson TEXT NOT NULL,
              PRIMARY KEY(SnapshotId, StaffUid), FOREIGN KEY(SnapshotId) REFERENCES FmsaveImportRuns(SnapshotId)
            );
            CREATE TABLE IF NOT EXISTS FmsaveClubDirectory (
              SnapshotId INTEGER NOT NULL, ClubUid TEXT NOT NULL, Name TEXT NOT NULL, Nation TEXT NULL,
              RawJson TEXT NOT NULL, PRIMARY KEY(SnapshotId, ClubUid),
              FOREIGN KEY(SnapshotId) REFERENCES FmsaveImportRuns(SnapshotId)
            );
            CREATE TABLE IF NOT EXISTS FmsaveExtraTables (
              SnapshotId INTEGER NOT NULL, TableName TEXT NOT NULL, RowIndex INTEGER NOT NULL, RawJson TEXT NOT NULL,
              PRIMARY KEY(SnapshotId, TableName, RowIndex), FOREIGN KEY(SnapshotId) REFERENCES FmsaveImportRuns(SnapshotId)
            );
            CREATE INDEX IF NOT EXISTS IX_FmsavePlayers_Snapshot ON FmsavePlayerSnapshots(SnapshotId, PlayerUid);
            CREATE INDEX IF NOT EXISTS IX_FmsaveStaff_Snapshot ON FmsaveStaffDirectory(SnapshotId, Name);
            CREATE INDEX IF NOT EXISTS IX_FmsaveClubs_Snapshot ON FmsaveClubDirectory(SnapshotId, Name);
            CREATE INDEX IF NOT EXISTS IX_FmsaveExtra_Snapshot ON FmsaveExtraTables(SnapshotId, TableName);
            CREATE TABLE IF NOT EXISTS FmsaveStatFacts (
              SnapshotId INTEGER NOT NULL, TableName TEXT NOT NULL, RowIndex INTEGER NOT NULL,
              PlayerUid TEXT NOT NULL, PeriodKey TEXT NOT NULL, Minutes REAL NULL,
              FieldKey TEXT NOT NULL, NumericValue REAL NOT NULL,
              PRIMARY KEY(SnapshotId,TableName,RowIndex,FieldKey),
              FOREIGN KEY(SnapshotId) REFERENCES FmsaveImportRuns(SnapshotId)
            );
            CREATE INDEX IF NOT EXISTS IX_FmsaveStatFacts_Catalog ON FmsaveStatFacts(SnapshotId,FieldKey,TableName);
            CREATE INDEX IF NOT EXISTS IX_FmsaveStatFacts_PlayerPeriod ON FmsaveStatFacts(SnapshotId,PlayerUid,TableName,PeriodKey);
            CREATE TABLE IF NOT EXISTS ExternalStatImportRuns (
              ImportId INTEGER PRIMARY KEY AUTOINCREMENT, FileName TEXT NOT NULL, ImportedUtc TEXT NOT NULL,
              RowsSeen INTEGER NOT NULL DEFAULT 0, RowsImported INTEGER NOT NULL DEFAULT 0, Notes TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS ExternalStatRows (
              ImportId INTEGER NOT NULL, RowIndex INTEGER NOT NULL, PlayerIdentity TEXT NOT NULL,
              PlayerSourceId TEXT NOT NULL, PlayerName TEXT NOT NULL, Club TEXT NULL, Season TEXT NOT NULL,
              Minutes REAL NULL, RawJson TEXT NOT NULL, PRIMARY KEY(ImportId, RowIndex),
              FOREIGN KEY(ImportId) REFERENCES ExternalStatImportRuns(ImportId)
            );
            CREATE INDEX IF NOT EXISTS IX_ExternalStatRows_ImportPlayer ON ExternalStatRows(ImportId, PlayerIdentity);
            CREATE INDEX IF NOT EXISTS IX_ExternalStatRows_Source ON ExternalStatRows(PlayerSourceId);
            """;
        await snapshotSchema.ExecuteNonQueryAsync();
        await SeedFieldTargetsAsync(db);
        await EnsureColumnAsync(db, "LivePlayers", "TeamObjectAddress", "TEXT NULL");
        await EnsureColumnAsync(db, "LivePlayers", "DateOfBirth", "TEXT NULL");
        await EnsureColumnAsync(db, "LivePlayers", "RegenStatus", "TEXT NOT NULL DEFAULT 'Unknown'");
        await EnsureColumnAsync(db, "LivePlayers", "RegenEvidence", "TEXT NULL");
        await EnsureColumnAsync(db, "LivePlayers", "RegenConfidence", "TEXT NOT NULL DEFAULT 'Unverified'");
        await EnsureColumnAsync(db, "LivePlayers", "NameValidation", "TEXT NOT NULL DEFAULT 'Candidate'");
        await EnsureColumnAsync(db, "LivePlayers", "RelationshipValidation", "TEXT NOT NULL DEFAULT 'Candidate'");
        await EnsureColumnAsync(db, "Players", "League", "TEXT NULL");
        await EnsureColumnAsync(db, "Players", "HomeGrownClub", "INTEGER NULL");
        await EnsureColumnAsync(db, "Players", "HomeGrownNation", "INTEGER NULL");
        await EnsureColumnAsync(db, "FmsavePlayerSnapshots", "PlayerSourceId", "TEXT NULL");
        await EnsureColumnAsync(db, "FmsaveImportRuns", "Game", "TEXT NULL");
        await EnsureColumnAsync(db, "FmsaveImportRuns", "GameBuild", "TEXT NULL");
        await EnsureColumnAsync(db, "FmsaveImportRuns", "KnownBuild", "INTEGER NULL");
        await EnsureColumnAsync(db, "FmsaveImportRuns", "GameDate", "TEXT NULL");
        await EnsureColumnAsync(db, "FmsaveImportRuns", "ValidationJson", "TEXT NULL");
        await EnsureColumnAsync(db, "Players", "PreferredFoot", "TEXT NULL");
        await EnsureColumnAsync(db, "Players", "LeftFoot", "INTEGER NULL");
        await EnsureColumnAsync(db, "Players", "RightFoot", "INTEGER NULL");
        await EnsureColumnAsync(db, "Players", "SaveClubUid", "TEXT NULL");
        var clubIndex = db.CreateCommand();
        clubIndex.CommandText = "CREATE INDEX IF NOT EXISTS IX_Players_SaveClubUid ON Players(SaveClubUid);";
        await clubIndex.ExecuteNonQueryAsync();
        var index = db.CreateCommand();
        index.CommandText = """
            CREATE INDEX IF NOT EXISTS IX_Players_League ON Players(League);
            CREATE INDEX IF NOT EXISTS IX_Players_Name ON Players(Name COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS IX_Players_Club ON Players(Club COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS IX_PlayerAttributes_SourceName ON PlayerAttributes(SourceId, AttributeName);
            CREATE INDEX IF NOT EXISTS IX_PlayerAttributes_SourceNameNoCase ON PlayerAttributes(SourceId, AttributeName COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS IX_FmsavePlayerFacts_SnapshotPlayerGroupField ON FmsavePlayerFacts(SnapshotId, PlayerSourceId, FactGroup, FieldKey);
            CREATE INDEX IF NOT EXISTS IX_FmsavePlayerFacts_SnapshotGroupFieldPlayer ON FmsavePlayerFacts(SnapshotId, FactGroup, FieldKey, PlayerSourceId);
            CREATE INDEX IF NOT EXISTS IX_LivePlayers_Name ON LivePlayers(PlayerName COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS IX_LivePlayers_ClubUid ON LivePlayers(ClubUid);
            CREATE INDEX IF NOT EXISTS IX_LiveClubs_Name ON LiveClubs(ClubName COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS IX_LivePlayerFacts_PlayerField ON LivePlayerFacts(PlayerUid, FieldKey, ValidationStatus);
            CREATE INDEX IF NOT EXISTS IX_FmsavePlayerFacts_PlayerGroupField ON FmsavePlayerFacts(PlayerSourceId, FactGroup, FieldKey);
            """;
        await index.ExecuteNonQueryAsync();
    }


    private static async Task SeedFieldTargetsAsync(SqliteConnection db)
    {
        var targets = new (string Entity, string Key, string Name, string Type)[]
        {
            ("Player","TechnicalAttributes","Technical attributes","AttributeSet"),
            ("Player","MentalAttributes","Mental attributes","AttributeSet"),
            ("Player","PhysicalAttributes","Physical attributes","AttributeSet"),
            ("Player","GoalkeeperAttributes","Goalkeeper attributes","AttributeSet"),
            ("Player","CurrentAbility","Current Ability","Integer"),
            ("Player","PotentialAbility","Potential Ability","Integer"),
            ("Player","Reputation","FM27 reputation","Integer"),
            ("Player","PreferredFootStrength","Left/right foot strength","0-20 ratings"),
            ("Player","Positions","Position suitability","PositionRatings"),
            ("Player","Contract","Contract and wage","Structured"),
            ("Player","HiddenAttributes","Hidden attributes","AttributeSet"),
            ("Player","SeasonPerformance","Season performance","Structured"),
            ("Club","Finances","Finances and budgets","Structured"),
            ("Club","Reputation","FM27 reputation","Integer"),
            ("Club","Facilities","Training/youth facilities","Structured"),
            ("Club","Ownership","Ownership and board","Structured"),
            ("Club","Affiliations","Club affiliations","Relationships"),
            ("Club","Stadium","Stadium information","Structured"),
            ("Staff","Identity","Staff identity","Structured"),
            ("Staff","Attributes","Staff attributes","AttributeSet"),
            ("Staff","Contract","Staff contract","Structured"),
            ("World","Competitions","Leagues and competitions","EntitySet"),
            ("World","Nations","Nations and regions","EntitySet"),
            ("History","Transfers","Transfer history","Structured"),
            ("History","PlayerCareer","Player career history","Structured")
        };
        foreach (var item in targets)
        {
            var add = db.CreateCommand();
            add.CommandText = "INSERT OR IGNORE INTO LiveFieldRegistry(EntityType,FieldKey,DisplayName,DataType,ValidationStatus,Evidence) VALUES($entity,$key,$name,$type,'Unverified','Discovery target only; no memory offset or value has been validated yet.');";
            add.Parameters.AddWithValue("$entity", item.Entity); add.Parameters.AddWithValue("$key", item.Key);
            add.Parameters.AddWithValue("$name", item.Name); add.Parameters.AddWithValue("$type", item.Type);
            await add.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureColumnAsync(SqliteConnection db, string table, string column, string definition)
    {
        var check = db.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await check.ExecuteReaderAsync();
        while (await reader.ReadAsync().ConfigureAwait(false))
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
        await reader.DisposeAsync();
        var alter = db.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        await alter.ExecuteNonQueryAsync();
    }

    public async Task<List<PlayerRecord>> SearchAsync(string? search, int? minAge, int? maxAge,
        string? position, string? league, string? nation, string? homeGrown, string? preferredFoot = null, string? attributeName = null, double? minAttribute = null, double? maxAttribute = null, int? minLeftFoot = null, int? maxLeftFoot = null, int? minRightFoot = null, int? maxRightFoot = null, int? minCa = null, int? maxCa = null, int? minPa = null, int? maxPa = null, int limit = 1000, int offset = 0)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = """
          SELECT SourceId, Name, Age, Club, Nation, League, Position, CurrentAbility, PotentialAbility,
                 Value, HomeGrownClub, HomeGrownNation, DataOrigin, PreferredFoot, LeftFoot, RightFoot
          FROM Players
          WHERE ($search = '' OR Name LIKE $pattern OR COALESCE(Club,'') LIKE $pattern OR COALESCE(Nation,'') LIKE $pattern OR COALESCE(League,'') LIKE $pattern OR COALESCE(Position,'') LIKE $pattern OR SourceId LIKE $pattern)
            AND ($minAge IS NULL OR Age >= $minAge)
            AND ($maxAge IS NULL OR Age <= $maxAge)
            AND ($minCa IS NULL OR CurrentAbility >= $minCa)
            AND ($maxCa IS NULL OR CurrentAbility <= $maxCa)
            AND ($minPa IS NULL OR PotentialAbility >= $minPa)
            AND ($maxPa IS NULL OR PotentialAbility <= $maxPa)
            AND ($position IS NULL
              OR ($position='ATTACKERS' AND (Position IN ('ST','STC','AML','AMR') OR EXISTS (
                  SELECT 1 FROM FmsavePlayerFacts pf WHERE pf.PlayerSourceId=Players.SourceId AND pf.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)
                  AND pf.FactGroup IN ('natural_positions','accomplished_positions','positions') AND (UPPER(pf.FieldKey) IN ('ST','STC','AML','AMR') OR UPPER(COALESCE(pf.TextValue,'')) IN ('ST','STC','AML','AMR')))))
              OR ($position='MIDFIELDERS' AND (Position IN ('DM','ML','MC','MR','AML','AMC','AMR') OR EXISTS (
                  SELECT 1 FROM FmsavePlayerFacts pf WHERE pf.PlayerSourceId=Players.SourceId AND pf.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)
                  AND pf.FactGroup IN ('natural_positions','accomplished_positions','positions') AND (UPPER(pf.FieldKey) IN ('DM','ML','MC','MR','AML','AMC','AMR') OR UPPER(COALESCE(pf.TextValue,'')) IN ('DM','ML','MC','MR','AML','AMC','AMR')))))
              OR ($position='DEFENDERS' AND (Position IN ('SW','DL','DC','DR','WBL','WBR') OR EXISTS (
                  SELECT 1 FROM FmsavePlayerFacts pf WHERE pf.PlayerSourceId=Players.SourceId AND pf.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)
                  AND pf.FactGroup IN ('natural_positions','accomplished_positions','positions') AND (UPPER(pf.FieldKey) IN ('SW','DL','DC','DR','WBL','WBR') OR UPPER(COALESCE(pf.TextValue,'')) IN ('SW','DL','DC','DR','WBL','WBR')))))
              OR ($position='GOALKEEPERS' AND (Position='GK' OR EXISTS (
                  SELECT 1 FROM FmsavePlayerFacts pf WHERE pf.PlayerSourceId=Players.SourceId AND pf.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)
                  AND pf.FactGroup IN ('natural_positions','accomplished_positions','positions') AND (UPPER(pf.FieldKey)='GK' OR UPPER(COALESCE(pf.TextValue,''))='GK'))))
              OR ($position NOT IN ('ATTACKERS','MIDFIELDERS','DEFENDERS','GOALKEEPERS') AND (Position LIKE $positionPattern OR EXISTS (
                  SELECT 1 FROM FmsavePlayerFacts pf WHERE pf.PlayerSourceId=Players.SourceId AND pf.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)
                  AND pf.FactGroup IN ('natural_positions','accomplished_positions','positions') AND (pf.FieldKey LIKE $positionFieldPattern OR pf.TextValue LIKE $positionPattern)))))
            AND ($league = '' OR League LIKE $leaguePattern)
            AND ($nation = '' OR Nation LIKE $nationPattern)
            AND ($homeGrown = '' OR ($homeGrown = 'Club' AND HomeGrownClub = 1)
                 OR ($homeGrown = 'Nation' AND HomeGrownNation = 1))
            AND ($foot = '' OR PreferredFoot = $foot)
            AND ($minLeftFoot IS NULL OR LeftFoot >= $minLeftFoot)
            AND ($maxLeftFoot IS NULL OR LeftFoot <= $maxLeftFoot)
            AND ($minRightFoot IS NULL OR RightFoot >= $minRightFoot)
            AND ($maxRightFoot IS NULL OR RightFoot <= $maxRightFoot)
            AND ($attributeName = '' OR EXISTS (SELECT 1 FROM PlayerAttributes a WHERE a.SourceId=Players.SourceId AND a.AttributeName=$attributeName AND ($minAttribute IS NULL OR a.AttributeValue >= $minAttribute) AND ($maxAttribute IS NULL OR a.AttributeValue <= $maxAttribute)))
            AND (Players.SourceId NOT LIKE 'FMSAVE:%' OR EXISTS (SELECT 1 FROM FmsavePlayerSnapshots s WHERE s.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) AND s.PlayerSourceId=Players.SourceId))
          ORDER BY CASE WHEN $search <> '' AND Name LIKE $prefix THEN 0 ELSE 1 END, Name COLLATE NOCASE LIMIT $limit OFFSET $offset;
          """;
        cmd.Parameters.AddWithValue("$search", search ?? "");
        cmd.Parameters.AddWithValue("$pattern", $"%{search ?? ""}%");
        cmd.Parameters.AddWithValue("$prefix", $"{search ?? ""}%");
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100000));
        cmd.Parameters.AddWithValue("$offset", Math.Max(0, offset));
        cmd.Parameters.AddWithValue("$minAge", (object?)minAge ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$maxAge", (object?)maxAge ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$position", (object?)position ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$positionPattern", position is null ? DBNull.Value : $"%{position}%");
        cmd.Parameters.AddWithValue("$positionFieldPattern", position is null ? DBNull.Value : $"%{position.ToLowerInvariant()}");
        cmd.Parameters.AddWithValue("$minCa", (object?)minCa ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$maxCa", (object?)maxCa ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$minPa", (object?)minPa ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$maxPa", (object?)maxPa ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$league", league ?? "");
        cmd.Parameters.AddWithValue("$leaguePattern", $"%{league ?? ""}%");
        cmd.Parameters.AddWithValue("$nation", nation ?? "");
        cmd.Parameters.AddWithValue("$nationPattern", $"%{nation ?? ""}%");
        cmd.Parameters.AddWithValue("$homeGrown", homeGrown ?? "");
        cmd.Parameters.AddWithValue("$foot", preferredFoot ?? "");
        cmd.Parameters.AddWithValue("$minLeftFoot", (object?)minLeftFoot ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$maxLeftFoot", (object?)maxLeftFoot ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$minRightFoot", (object?)minRightFoot ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$maxRightFoot", (object?)maxRightFoot ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$attributeName", attributeName ?? "");
        cmd.Parameters.AddWithValue("$minAttribute", (object?)minAttribute ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$maxAttribute", (object?)maxAttribute ?? DBNull.Value);
        var result = new List<PlayerRecord>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            result.Add(new PlayerRecord {
                SourceId = reader.GetString(0), Name = reader.GetString(1),
                Age = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Club = reader.IsDBNull(3) ? null : reader.GetString(3),
                Nation = reader.IsDBNull(4) ? null : reader.GetString(4),
                League = reader.IsDBNull(5) ? null : reader.GetString(5),
                Position = reader.IsDBNull(6) ? null : reader.GetString(6),
                CurrentAbility = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                PotentialAbility = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Value = reader.IsDBNull(9) ? null : reader.GetDecimal(9),
                HomeGrownClub = reader.IsDBNull(10) ? null : reader.GetInt32(10) == 1,
                HomeGrownNation = reader.IsDBNull(11) ? null : reader.GetInt32(11) == 1,
                DataOrigin = reader.GetString(12),
                PreferredFoot = reader.IsDBNull(13) ? null : reader.GetString(13),
                LeftFoot = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                RightFoot = reader.IsDBNull(15) ? null : reader.GetInt32(15)
            });
        }
        return result;
    }

    public async Task<List<string>> GetNationalitiesAsync()
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT Nation FROM Players WHERE Nation IS NOT NULL AND TRIM(Nation) <> '' ORDER BY Nation;";
        var items = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false)) items.Add(reader.GetString(0));
        return items;
    }

    public async Task<List<string>> GetAttributeNamesAsync()
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT AttributeName FROM PlayerAttributes UNION SELECT substr(FieldKey,instr(FieldKey,'.')+1) FROM LivePlayerFacts WHERE instr(FieldKey,'.')>0 ORDER BY 1;";
        var items = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false)) items.Add(reader.GetString(0));
        return items;
    }

    public async Task SavePlayerAttributeAsync(string sourceId, string playerName, string attributeName, string category, double value, string changeSource = "FM Atlas local database only")
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        if (value < 1 || value > 20) throw new ArgumentOutOfRangeException(nameof(value), "FM attributes must be between 1 and 20.");
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        await using var tx = await db.BeginTransactionAsync();
        var old = db.CreateCommand(); old.Transaction = (SqliteTransaction)tx;
        old.CommandText = "SELECT AttributeValue FROM PlayerAttributes WHERE SourceId=$id AND AttributeName=$name;";
        old.Parameters.AddWithValue("$id", sourceId); old.Parameters.AddWithValue("$name", attributeName);
        var oldValue = await old.ExecuteScalarAsync();
        var save = db.CreateCommand(); save.Transaction = (SqliteTransaction)tx;
        save.CommandText = "INSERT INTO PlayerAttributes(SourceId,AttributeName,Category,AttributeValue,UpdatedUtc) VALUES($id,$name,$category,$value,$utc) ON CONFLICT(SourceId,AttributeName) DO UPDATE SET Category=excluded.Category,AttributeValue=excluded.AttributeValue,UpdatedUtc=excluded.UpdatedUtc;";
        save.Parameters.AddWithValue("$id", sourceId); save.Parameters.AddWithValue("$name", attributeName); save.Parameters.AddWithValue("$category", category); save.Parameters.AddWithValue("$value", value); save.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        await save.ExecuteNonQueryAsync();
        var audit = db.CreateCommand(); audit.Transaction = (SqliteTransaction)tx;
        audit.CommandText = "INSERT INTO EditHistory(SourceId,PlayerName,FieldName,OldValue,NewValue,ChangedUtc,ChangeSource) VALUES($id,$player,$field,$old,$new,$utc,$source);";
        audit.Parameters.AddWithValue("$id", sourceId); audit.Parameters.AddWithValue("$player", playerName); audit.Parameters.AddWithValue("$field", attributeName); audit.Parameters.AddWithValue("$old", oldValue is null || oldValue == DBNull.Value ? DBNull.Value : Convert.ToString(oldValue, System.Globalization.CultureInfo.InvariantCulture)); audit.Parameters.AddWithValue("$new", value.ToString(System.Globalization.CultureInfo.InvariantCulture)); audit.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O")); audit.Parameters.AddWithValue("$source", changeSource);
        await audit.ExecuteNonQueryAsync();
        await tx.CommitAsync().ConfigureAwait(false);
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task SaveLocalAbilitiesAsync(string sourceId, string playerName, int currentAbility, int potentialAbility, string changeSource = "FM Atlas local database only")
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        if (currentAbility is < 0 or > 200) throw new ArgumentOutOfRangeException(nameof(currentAbility), "CA must be between 0 and 200.");
        if (potentialAbility is < 0 or > 200) throw new ArgumentOutOfRangeException(nameof(potentialAbility), "PA must be between 0 and 200.");
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        await using var tx = await db.BeginTransactionAsync().ConfigureAwait(false);
        foreach (var (field, value) in new[] { ("CurrentAbility", currentAbility), ("PotentialAbility", potentialAbility) })
        {
            var read = db.CreateCommand(); read.Transaction = (SqliteTransaction)tx;
            read.CommandText = $"SELECT {field} FROM Players WHERE SourceId=$id;"; read.Parameters.AddWithValue("$id", sourceId);
            var oldValue = await read.ExecuteScalarAsync().ConfigureAwait(false);
            if (oldValue is null) throw new InvalidOperationException("This player is not in the local player database. Import the player record first.");
            var update = db.CreateCommand(); update.Transaction = (SqliteTransaction)tx;
            update.CommandText = $"UPDATE Players SET {field}=$value,UpdatedUtc=$utc WHERE SourceId=$id;";
            update.Parameters.AddWithValue("$value", value); update.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O")); update.Parameters.AddWithValue("$id", sourceId);
            await update.ExecuteNonQueryAsync().ConfigureAwait(false);
            var audit = db.CreateCommand(); audit.Transaction = (SqliteTransaction)tx;
            audit.CommandText = "INSERT INTO EditHistory(SourceId,PlayerName,FieldName,OldValue,NewValue,ChangedUtc,ChangeSource) VALUES($id,$player,$field,$old,$new,$utc,$source);";
            audit.Parameters.AddWithValue("$id", sourceId); audit.Parameters.AddWithValue("$player", playerName); audit.Parameters.AddWithValue("$field", field);
            audit.Parameters.AddWithValue("$old", oldValue is null || oldValue == DBNull.Value ? DBNull.Value : Convert.ToString(oldValue, System.Globalization.CultureInfo.InvariantCulture));
            audit.Parameters.AddWithValue("$new", value.ToString(System.Globalization.CultureInfo.InvariantCulture)); audit.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O")); audit.Parameters.AddWithValue("$source", changeSource);
            await audit.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        await tx.CommitAsync().ConfigureAwait(false);
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task SavePlayerAttributeBatchAsync(string sourceId, string playerName, IReadOnlyList<(string AttributeName, string Category, double Value)> fields, string changeSource = "FM Atlas local database only")
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        if (fields.Count == 0) return;
        if (fields.Count > 60) throw new ArgumentOutOfRangeException(nameof(fields), "A single attribute batch is limited to 60 fields.");
        if (fields.Any(x => string.IsNullOrWhiteSpace(x.AttributeName) || x.Value is < 1 or > 20 || !double.IsFinite(x.Value)))
            throw new ArgumentException("Every attribute must have a name and a finite value from 1 to 20.", nameof(fields));
        if (fields.Select(x => x.AttributeName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != fields.Count)
            throw new ArgumentException("The same attribute cannot appear twice in one batch.", nameof(fields));
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        await using var tx = await db.BeginTransactionAsync().ConfigureAwait(false);
        foreach (var field in fields)
        {
            var read = db.CreateCommand(); read.Transaction = (SqliteTransaction)tx;
            read.CommandText = "SELECT AttributeValue FROM PlayerAttributes WHERE SourceId=$id AND AttributeName=$name;";
            read.Parameters.AddWithValue("$id", sourceId); read.Parameters.AddWithValue("$name", field.AttributeName);
            var oldValue = await read.ExecuteScalarAsync().ConfigureAwait(false);
            if (oldValue is null || oldValue == DBNull.Value) throw new InvalidOperationException($"The attribute {field.AttributeName} is not present in Atlas's current snapshot. Refresh the source data before editing it.");
            var update = db.CreateCommand(); update.Transaction = (SqliteTransaction)tx;
            update.CommandText = "UPDATE PlayerAttributes SET Category=$category,AttributeValue=$value,UpdatedUtc=$utc WHERE SourceId=$id AND AttributeName=$name;";
            update.Parameters.AddWithValue("$category", field.Category); update.Parameters.AddWithValue("$value", field.Value);
            update.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O")); update.Parameters.AddWithValue("$id", sourceId); update.Parameters.AddWithValue("$name", field.AttributeName);
            var changed = await update.ExecuteNonQueryAsync().ConfigureAwait(false);
            if (changed != 1) throw new InvalidOperationException($"The attribute {field.AttributeName} changed or disappeared while Atlas was saving the batch.");
            var audit = db.CreateCommand(); audit.Transaction = (SqliteTransaction)tx;
            audit.CommandText = "INSERT INTO EditHistory(SourceId,PlayerName,FieldName,OldValue,NewValue,ChangedUtc,ChangeSource) VALUES($id,$player,$field,$old,$new,$utc,$source);";
            audit.Parameters.AddWithValue("$id", sourceId); audit.Parameters.AddWithValue("$player", playerName); audit.Parameters.AddWithValue("$field", field.AttributeName);
            audit.Parameters.AddWithValue("$old", Convert.ToString(oldValue, System.Globalization.CultureInfo.InvariantCulture)!);
            audit.Parameters.AddWithValue("$new", field.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            audit.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O")); audit.Parameters.AddWithValue("$source", changeSource);
            await audit.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        await tx.CommitAsync().ConfigureAwait(false);
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task SaveLocalAbilityAsync(string sourceId, string playerName, string fieldName, int? value)
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        var column = fieldName switch { "CurrentAbility" => "CurrentAbility", "PotentialAbility" => "PotentialAbility", _ => throw new ArgumentOutOfRangeException(nameof(fieldName)) };
        if (value is < 0 or > 200) throw new ArgumentOutOfRangeException(nameof(value), "CA and PA must be between 0 and 200.");
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        await using var tx = await db.BeginTransactionAsync();
        var old = db.CreateCommand(); old.Transaction = (SqliteTransaction)tx; old.CommandText = $"SELECT {column} FROM Players WHERE SourceId=$id;"; old.Parameters.AddWithValue("$id", sourceId);
        var oldValue = await old.ExecuteScalarAsync();
        if (oldValue is null) throw new InvalidOperationException("This player is not in the local player database. Import the player record first.");
        var update = db.CreateCommand(); update.Transaction = (SqliteTransaction)tx; update.CommandText = $"UPDATE Players SET {column}=$value,UpdatedUtc=$utc WHERE SourceId=$id;";
        update.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value); update.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O")); update.Parameters.AddWithValue("$id", sourceId); await update.ExecuteNonQueryAsync();
        var audit = db.CreateCommand(); audit.Transaction = (SqliteTransaction)tx;
        audit.CommandText = "INSERT INTO EditHistory(SourceId,PlayerName,FieldName,OldValue,NewValue,ChangedUtc,ChangeSource) VALUES($id,$player,$field,$old,$new,$utc,'FM Atlas local database only');";
        audit.Parameters.AddWithValue("$id", sourceId); audit.Parameters.AddWithValue("$player", playerName); audit.Parameters.AddWithValue("$field", column);
        audit.Parameters.AddWithValue("$old", oldValue is null || oldValue == DBNull.Value ? DBNull.Value : Convert.ToString(oldValue));
        audit.Parameters.AddWithValue("$new", (object?)value?.ToString() ?? DBNull.Value); audit.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O")); await audit.ExecuteNonQueryAsync();
        await tx.CommitAsync().ConfigureAwait(false);
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task UndoLocalEditAsync(long historyId)
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false); await using var tx = await db.BeginTransactionAsync();
        var read = db.CreateCommand(); read.Transaction = (SqliteTransaction)tx;
        read.CommandText = "SELECT SourceId,PlayerName,FieldName,OldValue,NewValue,ChangeSource FROM EditHistory WHERE Id=$id;"; read.Parameters.AddWithValue("$id", historyId);
        string sourceId, playerName, fieldName, originalChangeSource; string? oldValue, newValue;
        await using (var reader = await read.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync().ConfigureAwait(false)) throw new InvalidOperationException("The selected edit history entry no longer exists.");
            sourceId=reader.GetString(0); playerName=reader.GetString(1); fieldName=reader.GetString(2);
            oldValue=reader.IsDBNull(3)?null:reader.GetString(3); newValue=reader.IsDBNull(4)?null:reader.GetString(4);
            originalChangeSource=reader.IsDBNull(5)?"FM Atlas local database only":reader.GetString(5);
        }
        if (fieldName is "CurrentAbility" or "PotentialAbility")
        {
            var current = db.CreateCommand(); current.Transaction = (SqliteTransaction)tx; current.CommandText = $"SELECT {fieldName} FROM Players WHERE SourceId=$id;"; current.Parameters.AddWithValue("$id", sourceId);
            var currentValue = await current.ExecuteScalarAsync().ConfigureAwait(false);
            var currentText = currentValue is null || currentValue == DBNull.Value ? null : Convert.ToString(currentValue, System.Globalization.CultureInfo.InvariantCulture);
            if (!string.Equals(currentText, newValue, StringComparison.Ordinal)) throw new InvalidOperationException("Undo blocked: this field has changed again since the selected edit. Refresh history and undo the newest change first.");
            var update=db.CreateCommand(); update.Transaction=(SqliteTransaction)tx; update.CommandText=$"UPDATE Players SET {fieldName}=$value,UpdatedUtc=$utc WHERE SourceId=$id;";
            update.Parameters.AddWithValue("$value", (object?)oldValue ?? DBNull.Value); update.Parameters.AddWithValue("$utc",DateTime.UtcNow.ToString("O")); update.Parameters.AddWithValue("$id",sourceId); await update.ExecuteNonQueryAsync();
        }
        else
        {
            var current = db.CreateCommand(); current.Transaction = (SqliteTransaction)tx; current.CommandText = "SELECT AttributeValue FROM PlayerAttributes WHERE SourceId=$id AND AttributeName=$field;"; current.Parameters.AddWithValue("$id", sourceId); current.Parameters.AddWithValue("$field", fieldName);
            var currentValue = await current.ExecuteScalarAsync().ConfigureAwait(false);
            var currentText = currentValue is null || currentValue == DBNull.Value ? null : Convert.ToDouble(currentValue, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!string.Equals(currentText, newValue, StringComparison.Ordinal)) throw new InvalidOperationException("Undo blocked: this attribute has changed again since the selected edit. Refresh history and undo the newest change first.");
            if (oldValue is null)
            {
                var delete=db.CreateCommand(); delete.Transaction=(SqliteTransaction)tx; delete.CommandText="DELETE FROM PlayerAttributes WHERE SourceId=$id AND AttributeName=$field;";
                delete.Parameters.AddWithValue("$id",sourceId); delete.Parameters.AddWithValue("$field",fieldName); await delete.ExecuteNonQueryAsync();
            }
            else
            {
                if (!double.TryParse(oldValue,System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out var restored)) throw new InvalidOperationException("The old attribute value is not numeric; the edit cannot be undone automatically.");
                var update=db.CreateCommand(); update.Transaction=(SqliteTransaction)tx; update.CommandText="UPDATE PlayerAttributes SET AttributeValue=$value,UpdatedUtc=$utc WHERE SourceId=$id AND AttributeName=$field;";
                update.Parameters.AddWithValue("$value",restored); update.Parameters.AddWithValue("$utc",DateTime.UtcNow.ToString("O")); update.Parameters.AddWithValue("$id",sourceId); update.Parameters.AddWithValue("$field",fieldName); await update.ExecuteNonQueryAsync();
            }
        }
        var audit=db.CreateCommand(); audit.Transaction=(SqliteTransaction)tx;
        audit.CommandText="INSERT INTO EditHistory(SourceId,PlayerName,FieldName,OldValue,NewValue,ChangedUtc,ChangeSource) VALUES($id,$player,$field,$old,$new,$utc,$source);";
        audit.Parameters.AddWithValue("$id",sourceId); audit.Parameters.AddWithValue("$player",playerName); audit.Parameters.AddWithValue("$field",fieldName);
        audit.Parameters.AddWithValue("$old",(object?)newValue??DBNull.Value); audit.Parameters.AddWithValue("$new",(object?)oldValue??DBNull.Value);
        audit.Parameters.AddWithValue("$utc",DateTime.UtcNow.ToString("O"));
        var undoSource = originalChangeSource.Contains("FM27 live memory", StringComparison.OrdinalIgnoreCase)
            ? $"FM27 live memory · guarded undo of #{historyId}"
            : $"Undo of local edit #{historyId}";
        audit.Parameters.AddWithValue("$source", undoSource); await audit.ExecuteNonQueryAsync();
        await tx.CommitAsync().ConfigureAwait(false);
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task<Dictionary<string, double>> GetPlayerAttributesAsync(string sourceId)
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT CASE WHEN Category LIKE 'attributes:%' THEN substr(Category,instr(Category,':')+1)||' · '||AttributeName WHEN Category='personality' THEN 'Personality · '||AttributeName ELSE AttributeName END,AttributeValue FROM PlayerAttributes WHERE SourceId=$id ORDER BY Category,AttributeName;";
        cmd.Parameters.AddWithValue("$id", sourceId);
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false); while (await reader.ReadAsync().ConfigureAwait(false)) result[reader.GetString(0)] = reader.GetDouble(1);
        return result;
    }

    public async Task<int> ImportPlayersCsvAsync(string path)
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8);
        if (lines.Length < 2) return 0;
        var headers = ParseCsvLine(lines[0]).Select((h,i)=>(h,i)).ToDictionary(x=>x.h.Trim(),x=>x.i,StringComparer.OrdinalIgnoreCase);
        string? Get(string[] row, string key) => headers.TryGetValue(key,out var i) && i < row.Length && !string.IsNullOrWhiteSpace(row[i]) ? row[i].Trim() : null;
        int? Int(string[] row,string key) => int.TryParse(Get(row,key),out var n) ? n : null;
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false); await using var tx = await db.BeginTransactionAsync();
        var count=0;
        for(var line=1;line<lines.Length;line++)
        {
            if(string.IsNullOrWhiteSpace(lines[line])) continue;
            var row=ParseCsvLine(lines[line]); var id=Get(row,"SourceId") ?? Get(row,"Id"); var name=Get(row,"Name");
            if(string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;
            var cmd=db.CreateCommand(); cmd.Transaction=(SqliteTransaction)tx;
            cmd.CommandText="INSERT INTO Players(SourceId,Name,Age,Club,Nation,League,Position,CurrentAbility,PotentialAbility,Value,HomeGrownClub,HomeGrownNation,PreferredFoot,LeftFoot,RightFoot,DataOrigin,UpdatedUtc) VALUES($id,$name,$age,$club,$nation,$league,$position,$ca,$pa,$value,$hgclub,$hgnation,$foot,$leftFoot,$rightFoot,'CSV import', $utc) ON CONFLICT(SourceId) DO UPDATE SET Name=excluded.Name,Age=excluded.Age,Club=excluded.Club,Nation=excluded.Nation,League=excluded.League,Position=excluded.Position,CurrentAbility=excluded.CurrentAbility,PotentialAbility=excluded.PotentialAbility,Value=excluded.Value,HomeGrownClub=excluded.HomeGrownClub,HomeGrownNation=excluded.HomeGrownNation,PreferredFoot=excluded.PreferredFoot,LeftFoot=excluded.LeftFoot,RightFoot=excluded.RightFoot,DataOrigin=excluded.DataOrigin,UpdatedUtc=excluded.UpdatedUtc;";
            cmd.Parameters.AddWithValue("$id",id); cmd.Parameters.AddWithValue("$name",name);
            void P(string key, object? val)=>cmd.Parameters.AddWithValue(key,val??DBNull.Value);
            P("$age",Int(row,"Age")); P("$club",Get(row,"Club")); P("$nation",Get(row,"Nation")); P("$league",Get(row,"League")); P("$position",Get(row,"Position")); P("$ca",Int(row,"CurrentAbility")); P("$pa",Int(row,"PotentialAbility"));
            P("$value",decimal.TryParse(Get(row,"Value"),System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out var val)?val:null);
            P("$hgclub",bool.TryParse(Get(row,"HomeGrownClub"),out var hc)?(hc?1:0):null); P("$hgnation",bool.TryParse(Get(row,"HomeGrownNation"),out var hn)?(hn?1:0):null); P("$foot",Get(row,"PreferredFoot")); P("$leftFoot",Int(row,"LeftFoot")); P("$rightFoot",Int(row,"RightFoot")); P("$utc",DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync(); count++;
            foreach(var header in headers.Keys.Where(h=>h.Contains(':')))
            {
                var v=Get(row,header); if(double.TryParse(v,System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out var score))
                {
                    var parts=header.Split(':',2); var a=db.CreateCommand(); a.Transaction=(SqliteTransaction)tx; a.CommandText="INSERT INTO PlayerAttributes(SourceId,AttributeName,Category,AttributeValue,UpdatedUtc) VALUES($id,$name,$cat,$value,$utc) ON CONFLICT(SourceId,AttributeName) DO UPDATE SET Category=excluded.Category,AttributeValue=excluded.AttributeValue,UpdatedUtc=excluded.UpdatedUtc;";
                    a.Parameters.AddWithValue("$id",id);a.Parameters.AddWithValue("$name",parts[1]);a.Parameters.AddWithValue("$cat",parts[0]);a.Parameters.AddWithValue("$value",score);a.Parameters.AddWithValue("$utc",DateTime.UtcNow.ToString("O")); await a.ExecuteNonQueryAsync();
                }
            }
        }
        await tx.CommitAsync().ConfigureAwait(false); return count;
    
        }
        finally
        {
            WriteGate.Release();
        }
}


    /// <summary>Imports an R3.4 full-intelligence CSV into the existing live-player fact store.
    /// Matches strictly by FM player UID; does not create or overwrite player identities.</summary>
    public async Task<(int MatchedPlayers, int FactsImported)> ImportFullIntelligenceReportAsync(string path)
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8);
        var headerLine = Array.FindIndex(lines, x => x.StartsWith("Player,UID,Section,Field,Offset,Raw,Decoded,Applicability,Confidence,Notes", StringComparison.OrdinalIgnoreCase));
        if (headerLine < 0) throw new InvalidDataException("This is not an FM Atlas R3.4 full-intelligence report. Expected the Player,UID,Section... header.");
        var header = ParseCsvLine(lines[headerLine]);
        var columns = header.Select((name,index)=>(name,index)).ToDictionary(x=>x.name.Trim(),x=>x.index,StringComparer.OrdinalIgnoreCase);
        string? Get(string[] row,string name) => columns.TryGetValue(name,out var i) && i<row.Length && !string.IsNullOrWhiteSpace(row[i]) ? row[i].Trim() : null;
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        var reportHashLine=lines.FirstOrDefault(x=>x.StartsWith("Module SHA-256:",StringComparison.OrdinalIgnoreCase));
        var reportHash=reportHashLine?.Split(':',2).LastOrDefault()?.Trim();
        if(string.IsNullOrWhiteSpace(reportHash)) throw new InvalidDataException("The intelligence report has no module fingerprint. It cannot be safely linked to this live database.");
        var runCmd=db.CreateCommand(); runCmd.CommandText="SELECT RunId,ModuleSha256 FROM LiveImportRuns WHERE Status='Completed' ORDER BY RunId DESC LIMIT 1;";
        await using var runReader=await runCmd.ExecuteReaderAsync();
        if(!await runReader.ReadAsync()) throw new InvalidOperationException("Import the live FM27 player database before importing the intelligence report. Player UIDs must already exist.");
        var runId=runReader.GetInt32(0); var liveHash=runReader.GetString(1); await runReader.DisposeAsync();
        if(!string.Equals(reportHash,liveHash,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"The report was captured from a different FM27 module build. Report: {reportHash}; latest live import: {liveHash}. Run a fresh player intelligence scan on the current build before importing it.");
        await using var tx = await db.BeginTransactionAsync();
        var matched = new HashSet<long>(); var imported = 0;
        for (var i=headerLine+1;i<lines.Length;i++)
        {
            var line=lines[i]; if(string.IsNullOrWhiteSpace(line) || line.StartsWith("#",StringComparison.Ordinal)) continue;
            var row=ParseCsvLine(line); if(!long.TryParse(Get(row,"UID"),out var uid) || uid<=0) continue;
            var exists=db.CreateCommand(); exists.Transaction=(SqliteTransaction)tx; exists.CommandText="SELECT 1 FROM LivePlayers WHERE PlayerUid=$uid;"; exists.Parameters.AddWithValue("$uid",uid);
            if(await exists.ExecuteScalarAsync() is null) continue;
            var section=Get(row,"Section") ?? "Other"; var field=Get(row,"Field") ?? Get(row,"Section") ?? "Unknown";
            var decoded=Get(row,"Decoded"); var raw=Get(row,"Raw"); var confidence=Get(row,"Confidence") ?? "Unspecified";
            double? numeric=double.TryParse(decoded,System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out var n)?n:null;
            // Ignore non-data narrative rows, but retain all actual decoded fields including position ratings and ability.
            if(decoded is null && raw is null) continue;
            var key=$"{section}.{field}";
            var cmd=db.CreateCommand(); cmd.Transaction=(SqliteTransaction)tx;
            cmd.CommandText="INSERT INTO LivePlayerFacts(PlayerUid,FieldKey,NumericValue,TextValue,ValidationStatus,SourceRunId,UpdatedUtc) VALUES($uid,$key,$numeric,$text,$status,$run,$utc) ON CONFLICT(PlayerUid,FieldKey) DO UPDATE SET NumericValue=excluded.NumericValue,TextValue=excluded.TextValue,ValidationStatus=excluded.ValidationStatus,SourceRunId=excluded.SourceRunId,UpdatedUtc=excluded.UpdatedUtc;";
            cmd.Parameters.AddWithValue("$uid",uid); cmd.Parameters.AddWithValue("$key",key); cmd.Parameters.AddWithValue("$numeric",(object?)numeric??DBNull.Value);
            cmd.Parameters.AddWithValue("$text",(object?)decoded??DBNull.Value); cmd.Parameters.AddWithValue("$status",confidence); cmd.Parameters.AddWithValue("$run",runId); cmd.Parameters.AddWithValue("$utc",DateTime.UtcNow.ToString("O"));
            if(await cmd.ExecuteNonQueryAsync()>0){ matched.Add(uid); imported++; }
        }
        await tx.CommitAsync().ConfigureAwait(false);
        return (matched.Count,imported);
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task UpdateLivePlayerFactAsync(long playerUid, string fieldKey, double value, string validationStatus = "Verified-UserUIWitness-ReadBack")
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE LivePlayerFacts SET NumericValue=$value,TextValue=NULL,ValidationStatus=$status,UpdatedUtc=$utc WHERE PlayerUid=$uid AND FieldKey=$field;";
        cmd.Parameters.AddWithValue("$value", value); cmd.Parameters.AddWithValue("$status", validationStatus);
        cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O")); cmd.Parameters.AddWithValue("$uid", playerUid); cmd.Parameters.AddWithValue("$field", fieldKey);
        var changed = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        if (changed == 0) AtlasLog.Warning($"Live field fact was not present to update after a read-back verified write: uid={playerUid}, field={fieldKey}.");
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task<List<(string FieldKey,double? NumericValue,string? TextValue,string ValidationStatus)>> GetLivePlayerFactsAsync(long playerUid)
    {
        await using var db=new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        var cmd=db.CreateCommand(); cmd.CommandText="SELECT FieldKey,NumericValue,TextValue,ValidationStatus FROM LivePlayerFacts WHERE PlayerUid=$uid ORDER BY FieldKey;"; cmd.Parameters.AddWithValue("$uid",playerUid);
        var rows=new List<(string,double?,string?,string)>(); await using var r=await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while(await r.ReadAsync().ConfigureAwait(false)) rows.Add((r.GetString(0),r.IsDBNull(1)?null:r.GetDouble(1),r.IsDBNull(2)?null:r.GetString(2),r.GetString(3)));
        return rows;
    }

    private static string[] ParseCsvLine(string line, char delimiter = ',')
    {
        var values=new List<string>(); var current=new StringBuilder(); var quoted=false;
        for(var i=0;i<line.Length;i++){var c=line[i]; if(c=='"'){if(quoted && i+1<line.Length && line[i+1]=='"'){current.Append('"');i++;}else quoted=!quoted;}else if(c==delimiter&&!quoted){values.Add(current.ToString());current.Clear();}else current.Append(c);} values.Add(current.ToString()); return values.ToArray();
    }

    public async Task CreateShortlistAsync(string shortlistName)
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO ShortlistGroups(ShortlistName, CreatedUtc) VALUES($name, $date);";
        cmd.Parameters.AddWithValue("$name", shortlistName.Trim());
        cmd.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task<int> AddPlayersToShortlistAsync(IReadOnlyList<PlayerRecord> players, string shortlistName, string? notes = null)
    {
        ArgumentNullException.ThrowIfNull(players);
        var name = string.IsNullOrWhiteSpace(shortlistName) ? "My Shortlist" : shortlistName.Trim();
        var uniquePlayers = players
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.SourceId))
            .GroupBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
        if (uniquePlayers.Count == 0) return 0;

        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var db = new SqliteConnection(_connectionString);
            await db.OpenAsync().ConfigureAwait(false);
            await using var transaction = await db.BeginTransactionAsync().ConfigureAwait(false);

            var group = db.CreateCommand();
            group.Transaction = (SqliteTransaction)transaction;
            group.CommandText = "INSERT OR IGNORE INTO ShortlistGroups(ShortlistName, CreatedUtc) VALUES($name, $created);";
            group.Parameters.AddWithValue("$name", name);
            group.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
            await group.ExecuteNonQueryAsync().ConfigureAwait(false);

            var command = db.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO Shortlists(SourceId, ShortlistName, Notes, AddedUtc)
                VALUES($id, $name, $notes, $date)
                ON CONFLICT(SourceId, ShortlistName)
                DO UPDATE SET Notes=COALESCE(excluded.Notes,Shortlists.Notes);
                """;
            command.Parameters.Add("$id", SqliteType.Text);
            command.Parameters.AddWithValue("$name", name);
            command.Parameters.Add("$notes", SqliteType.Text);
            command.Parameters.Add("$date", SqliteType.Text);
            await command.PrepareAsync().ConfigureAwait(false);

            foreach (var player in uniquePlayers)
            {
                command.Parameters["$id"].Value = player.SourceId;
                command.Parameters["$notes"].Value = (object?)notes ?? DBNull.Value;
                command.Parameters["$date"].Value = DateTime.UtcNow.ToString("O");
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await transaction.CommitAsync().ConfigureAwait(false);
            return uniquePlayers.Count;
        }
        finally
        {
            WriteGate.Release();
        }
    }

    public async Task AddToShortlistAsync(PlayerRecord player, string shortlistName, string? notes = null)
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = """
          INSERT INTO Shortlists(SourceId, ShortlistName, Notes, AddedUtc)
          VALUES($id, $name, $notes, $date)
          ON CONFLICT(SourceId, ShortlistName) DO UPDATE SET Notes=excluded.Notes;
          """;
        cmd.Parameters.AddWithValue("$id", player.SourceId);
        cmd.Parameters.AddWithValue("$name", string.IsNullOrWhiteSpace(shortlistName) ? "My Shortlist" : shortlistName.Trim());
        cmd.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task<List<string>> GetShortlistNamesAsync()
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT ShortlistName FROM ShortlistGroups UNION SELECT DISTINCT ShortlistName FROM Shortlists ORDER BY ShortlistName;";
        var names = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false)) names.Add(reader.GetString(0));
        if (names.Count == 0) names.Add("My Shortlist");
        return names;
    }

    public async Task<List<ShortlistRecord>> GetShortlistAsync(string shortlistName)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = """
          SELECT s.SourceId, COALESCE(p.Name,lp.PlayerName), s.ShortlistName, s.Notes, s.AddedUtc, COALESCE(p.Club,c.ClubName), p.Position
          FROM Shortlists s LEFT JOIN Players p ON p.SourceId=s.SourceId
          LEFT JOIN LivePlayers lp ON s.SourceId=('LIVE:' || lp.PlayerUid)
          LEFT JOIN LiveClubs c ON c.ClubUid=lp.ClubUid
          WHERE s.ShortlistName=$name ORDER BY COALESCE(p.Name,lp.PlayerName);
          """;
        cmd.Parameters.AddWithValue("$name", shortlistName);
        var rows = new List<ShortlistRecord>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false)) rows.Add(new ShortlistRecord {
            SourceId = reader.GetString(0), PlayerName = reader.IsDBNull(1) ? "Unknown player" : reader.GetString(1),
            ShortlistName = reader.GetString(2), Notes = reader.IsDBNull(3) ? null : reader.GetString(3),
            AddedUtc = DateTime.TryParse(reader.GetString(4), out var dt) ? dt : DateTime.MinValue,
            DisplayClub = reader.IsDBNull(5) ? "—" : reader.GetString(5),
            DisplayPosition = reader.IsDBNull(6) ? "—" : reader.GetString(6)
        });
        return rows;
    }

    public async Task RemoveFromShortlistAsync(string sourceId, string shortlistName)
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM Shortlists WHERE SourceId=$id AND ShortlistName=$name;";
        cmd.Parameters.AddWithValue("$id", sourceId);
        cmd.Parameters.AddWithValue("$name", shortlistName);
        await cmd.ExecuteNonQueryAsync();
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task<LiveImportResult> ImportLiveSnapshotAsync(FmAtlas.Services.LiveImportPayload payload)
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        await using var tx = await db.BeginTransactionAsync();
        var now = DateTime.UtcNow.ToString("O");
        var run = db.CreateCommand(); run.Transaction = (SqliteTransaction)tx;
        run.CommandText = "INSERT INTO LiveImportRuns(GameVersion,ModuleSha256,StartedUtc,CompletedUtc,Status,PlayersSeen,ClubsSeen) VALUES($v,$h,$s,NULL,'Importing',$p,$c); SELECT last_insert_rowid();";
        run.Parameters.AddWithValue("$v", payload.GameVersion); run.Parameters.AddWithValue("$h", payload.ModuleSha256);
        run.Parameters.AddWithValue("$s", now); run.Parameters.AddWithValue("$p", payload.PlayerSlots); run.Parameters.AddWithValue("$c", payload.Clubs.Count);
        var runId = Convert.ToInt32(await run.ExecuteScalarAsync());

        foreach (var club in payload.Clubs)
        {
            var cmd = db.CreateCommand(); cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = "INSERT INTO LiveClubs(ClubUid,ClubName,ShortName,ArrayIndex,ObjectAddress,SourceRunId,ValidationStatus,UpdatedUtc) VALUES($id,$name,NULL,$idx,$addr,$run,$status,$utc) ON CONFLICT(ClubUid) DO UPDATE SET ClubName=excluded.ClubName,ArrayIndex=excluded.ArrayIndex,ObjectAddress=excluded.ObjectAddress,SourceRunId=excluded.SourceRunId,ValidationStatus=CASE WHEN LiveClubs.ValidationStatus='WitnessVerified' OR excluded.ValidationStatus='WitnessVerified' THEN 'WitnessVerified' ELSE 'Candidate' END,UpdatedUtc=excluded.UpdatedUtc;";
            cmd.Parameters.AddWithValue("$id", (long)club.Uid); cmd.Parameters.AddWithValue("$name", club.Name); cmd.Parameters.AddWithValue("$idx", club.ArrayIndex);
            cmd.Parameters.AddWithValue("$addr", $"0x{club.Address:X}"); cmd.Parameters.AddWithValue("$run", runId);
            cmd.Parameters.AddWithValue("$status", club.WitnessVerified ? "WitnessVerified" : "Candidate"); cmd.Parameters.AddWithValue("$utc", now);
            await cmd.ExecuteNonQueryAsync();
        }
        foreach (var player in payload.Players.Where(p => p.TeamObjectAddress is not null).GroupBy(p => p.TeamObjectAddress).Select(g => g.First()))
        {
            var link = db.CreateCommand(); link.Transaction = (SqliteTransaction)tx;
            link.CommandText = "INSERT INTO LiveTeamLinks(TeamObjectAddress,ClubUid,SourceRunId,UpdatedUtc) VALUES($team,$club,$run,$utc) ON CONFLICT(TeamObjectAddress) DO UPDATE SET ClubUid=excluded.ClubUid,SourceRunId=excluded.SourceRunId,UpdatedUtc=excluded.UpdatedUtc;";
            link.Parameters.AddWithValue("$team", player.TeamObjectAddress!); link.Parameters.AddWithValue("$club", player.ClubUid.HasValue ? (object)(long)player.ClubUid.Value : DBNull.Value);
            link.Parameters.AddWithValue("$run", runId); link.Parameters.AddWithValue("$utc", now); await link.ExecuteNonQueryAsync();
        }
        var clubNames = payload.Clubs.ToDictionary(x => x.Uid, x => x.Name);
        var attributeUpsert = db.CreateCommand(); attributeUpsert.Transaction = (SqliteTransaction)tx;
        attributeUpsert.CommandText = "INSERT INTO PlayerAttributes(SourceId,AttributeName,Category,AttributeValue,UpdatedUtc) VALUES($id,$name,$category,$value,$utc) ON CONFLICT(SourceId,AttributeName) DO UPDATE SET Category=excluded.Category,AttributeValue=excluded.AttributeValue,UpdatedUtc=excluded.UpdatedUtc;";
        attributeUpsert.Parameters.Add("$id", SqliteType.Text); attributeUpsert.Parameters.Add("$name", SqliteType.Text);
        attributeUpsert.Parameters.Add("$category", SqliteType.Text); attributeUpsert.Parameters.Add("$value", SqliteType.Real); attributeUpsert.Parameters.Add("$utc", SqliteType.Text);
        var liveFactUpsert = db.CreateCommand(); liveFactUpsert.Transaction = (SqliteTransaction)tx;
        liveFactUpsert.CommandText = "INSERT INTO LivePlayerFacts(PlayerUid,FieldKey,NumericValue,TextValue,ValidationStatus,SourceRunId,UpdatedUtc) VALUES($uid,$key,$number,$text,$status,$run,$utc) ON CONFLICT(PlayerUid,FieldKey) DO UPDATE SET NumericValue=excluded.NumericValue,TextValue=excluded.TextValue,ValidationStatus=excluded.ValidationStatus,SourceRunId=excluded.SourceRunId,UpdatedUtc=excluded.UpdatedUtc;";
        liveFactUpsert.Parameters.Add("$uid", SqliteType.Integer); liveFactUpsert.Parameters.Add("$key", SqliteType.Text);
        liveFactUpsert.Parameters.Add("$number", SqliteType.Real); liveFactUpsert.Parameters.Add("$text", SqliteType.Text);
        liveFactUpsert.Parameters.Add("$status", SqliteType.Text); liveFactUpsert.Parameters.Add("$run", SqliteType.Integer); liveFactUpsert.Parameters.Add("$utc", SqliteType.Text);
        var atlasLivePlayerUpsert = db.CreateCommand(); atlasLivePlayerUpsert.Transaction = (SqliteTransaction)tx;
        atlasLivePlayerUpsert.CommandText = "INSERT INTO Players(SourceId,Name,Age,Club,Nation,League,Position,CurrentAbility,PotentialAbility,Value,HomeGrownClub,HomeGrownNation,PreferredFoot,DataOrigin,UpdatedUtc) VALUES($id,$name,$age,$club,NULL,NULL,NULL,$ca,$pa,NULL,NULL,NULL,NULL,$origin,$utc) ON CONFLICT(SourceId) DO UPDATE SET Name=excluded.Name,Age=COALESCE(excluded.Age,Players.Age),Club=excluded.Club,CurrentAbility=excluded.CurrentAbility,PotentialAbility=excluded.PotentialAbility,DataOrigin=excluded.DataOrigin,UpdatedUtc=excluded.UpdatedUtc;";
        foreach (var parameter in new[] { "$id", "$name", "$club", "$origin", "$utc" }) atlasLivePlayerUpsert.Parameters.Add(parameter, SqliteType.Text);
        atlasLivePlayerUpsert.Parameters.Add("$age", SqliteType.Integer); atlasLivePlayerUpsert.Parameters.Add("$ca", SqliteType.Integer); atlasLivePlayerUpsert.Parameters.Add("$pa", SqliteType.Integer);
        var liveAttributeCount = 0;
        foreach (var player in payload.Players)
        {
            var cmd = db.CreateCommand(); cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = "INSERT INTO LivePlayers(PlayerUid,PlayerName,TeamUid,ClubUid,ArrayIndex,ObjectAddress,SourceRunId,NameValidation,RelationshipValidation,UpdatedUtc,TeamObjectAddress,DateOfBirth,RegenStatus,RegenEvidence,RegenConfidence) VALUES($id,$name,NULL,$club,$idx,$addr,$run,'Decoded','Candidate',$utc,$team,$dob,'Unknown',$evidence,'Unverified') ON CONFLICT(PlayerUid) DO UPDATE SET PlayerName=excluded.PlayerName,ClubUid=excluded.ClubUid,ArrayIndex=excluded.ArrayIndex,ObjectAddress=excluded.ObjectAddress,SourceRunId=excluded.SourceRunId,NameValidation='Decoded',RelationshipValidation=CASE WHEN excluded.ClubUid IS NOT NULL THEN 'LayoutValidated' ELSE 'Unresolved' END,UpdatedUtc=excluded.UpdatedUtc,TeamObjectAddress=excluded.TeamObjectAddress,DateOfBirth=COALESCE(excluded.DateOfBirth,LivePlayers.DateOfBirth),RegenEvidence=excluded.RegenEvidence;";
            cmd.Parameters.AddWithValue("$id", (long)player.Uid); cmd.Parameters.AddWithValue("$name", player.Name);
            cmd.Parameters.AddWithValue("$club", player.ClubUid.HasValue ? (object)(long)player.ClubUid.Value : DBNull.Value); cmd.Parameters.AddWithValue("$idx", player.ArrayIndex);
            cmd.Parameters.AddWithValue("$addr", $"0x{player.Address:X}"); cmd.Parameters.AddWithValue("$run", runId); cmd.Parameters.AddWithValue("$utc", now);
            cmd.Parameters.AddWithValue("$team", (object?)player.TeamObjectAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$dob", player.DateOfBirth?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$evidence", player.RegenEvidence);
            await cmd.ExecuteNonQueryAsync();

            var sourceId = "LIVE:" + player.Uid.ToString(System.Globalization.CultureInfo.InvariantCulture);
            atlasLivePlayerUpsert.Parameters["$id"].Value = sourceId;
            atlasLivePlayerUpsert.Parameters["$name"].Value = player.Name;
            atlasLivePlayerUpsert.Parameters["$age"].Value = payload.GameDate.HasValue && player.DateOfBirth.HasValue ? CalculateAge(player.DateOfBirth.Value, payload.GameDate.Value) : DBNull.Value;
            atlasLivePlayerUpsert.Parameters["$club"].Value = player.ClubUid.HasValue && clubNames.TryGetValue(player.ClubUid.Value, out var clubName) ? clubName : DBNull.Value;
            atlasLivePlayerUpsert.Parameters["$ca"].Value = (object?)player.CurrentAbility ?? DBNull.Value;
            atlasLivePlayerUpsert.Parameters["$pa"].Value = (object?)player.PotentialAbility ?? DBNull.Value;
            atlasLivePlayerUpsert.Parameters["$origin"].Value = "FM27 Live · exact module profile matched · field-level values provisional";
            atlasLivePlayerUpsert.Parameters["$utc"].Value = now;
            await atlasLivePlayerUpsert.ExecuteNonQueryAsync();

            // Keep all live values in a separate fact store with explicit provisional status.
            // They are visible for investigation, but do not count as independently verified write permissions.
            void SetLiveFact(string key, double? numeric, string? textValue = null)
            {
                liveFactUpsert.Parameters["$uid"].Value = (long)player.Uid;
                liveFactUpsert.Parameters["$key"].Value = key;
                liveFactUpsert.Parameters["$number"].Value = (object?)numeric ?? DBNull.Value;
                liveFactUpsert.Parameters["$text"].Value = (object?)textValue ?? DBNull.Value;
                liveFactUpsert.Parameters["$status"].Value = "Provisional-ProfileMatched-Needs-UI-Witness";
                liveFactUpsert.Parameters["$run"].Value = runId;
                liveFactUpsert.Parameters["$utc"].Value = now;
                liveFactUpsert.ExecuteNonQuery();
            }
            if (player.CurrentAbility.HasValue) SetLiveFact("ability.current_ability", player.CurrentAbility.Value);
            if (player.PotentialAbility.HasValue) SetLiveFact("ability.potential_ability", player.PotentialAbility.Value);
            foreach (var field in player.Attributes ?? new Dictionary<string, double>())
            {
                SetLiveFact("attributes." + field.Key, field.Value);
                attributeUpsert.Parameters["$id"].Value = sourceId;
                attributeUpsert.Parameters["$name"].Value = field.Key;
                var schemaField = PlayerAttributeSchema.Fields.FirstOrDefault(x => x.Key.Equals(field.Key, StringComparison.OrdinalIgnoreCase));
                attributeUpsert.Parameters["$category"].Value = "attributes:" + (schemaField?.Category ?? "Other");
                attributeUpsert.Parameters["$value"].Value = field.Value;
                attributeUpsert.Parameters["$utc"].Value = now;
                await attributeUpsert.ExecuteNonQueryAsync(); liveAttributeCount++;
            }
            foreach (var field in player.Personality ?? new Dictionary<string, double>())
            {
                SetLiveFact("personality." + field.Key, field.Value);
                attributeUpsert.Parameters["$id"].Value = sourceId;
                attributeUpsert.Parameters["$name"].Value = field.Key;
                attributeUpsert.Parameters["$category"].Value = "personality";
                attributeUpsert.Parameters["$value"].Value = field.Value;
                attributeUpsert.Parameters["$utc"].Value = now;
                await attributeUpsert.ExecuteNonQueryAsync(); liveAttributeCount++;
            }
            foreach (var field in player.PositionRatings ?? new Dictionary<string, double>())
                SetLiveFact("positions." + field.Key, field.Value);
        }
        var registryRows = new[]
        {
            ("Player", "Uid", "Player UID", "+0x294", "UInt32", "ProfileMatched-Provisional", "Player-array entries decoded under an exact module fingerprint; uniqueness and user-visible identity still require a live UI witness."),
            ("Player", "Name", "Player name", "+0x2D8/+0x2E0/+0x2E8", "UTF-8", "ProfileMatched-Provisional", "Name-table subrecords decoded under the exact module profile; sampled names must be matched against the loaded game UI."),
            ("Player", "TeamObjectAddress", "Team object link", "+0x130", "Pointer", "ProfileMatched-Provisional", "Candidate player-to-team pointer read; club relationship is not promoted to verified until checked against in-game squad membership."),
            ("Team", "ClubObjectLink", "Team-to-club link", "+0x30", "Pointer", "ProfileMatched-Provisional", "Candidate team-to-club pointer read from the FM27 layout cross-reference; not independently witnessed by Atlas."),
            ("Club", "Uid", "Club UID", "+0x0C", "UInt32", "ProfileMatched-Provisional", "Club-array entries decoded under an exact module fingerprint; individual club identity still needs UI comparison."),
            ("Club", "Name", "Club name", "+0xC0", "LengthPrefixedUtf8", "ProfileMatched-Provisional", "Club name pointers decoded under the exact module profile; user-visible name matching remains a validation step."),
            ("Player", "CurrentAbility", "Current Ability", "+0x264", "UInt16", "ProfileMatched-Provisional", "Candidate live field from the FM27 field-layout cross-reference; must be checked against an independent editor before treating as verified."),
            ("Player", "PotentialAbility", "Potential Ability", "+0x266", "Int16", "ProfileMatched-Provisional", "Candidate live field from the FM27 field-layout cross-reference; negative PA ranges are not imported as a scalar value."),
            ("Player", "Attributes", "Player attributes 1–20", "0x15F–0x194 field-specific", "EncodedByte", "ProfileMatched-Provisional", "Candidate attributes decoded as (raw+2)/5; field-level in-game UI witnesses are still required before writing."),
            ("Player", "PositionRatings", "Position ratings 1–20", "+0x150–0x15E", "UInt8", "ProfileMatched-Provisional", "Position field candidates retained for scouting display only; not currently write-enabled.")
        };
        foreach (var field in registryRows)
        {
            var reg = db.CreateCommand(); reg.Transaction = (SqliteTransaction)tx;
            reg.CommandText = "INSERT INTO LiveFieldRegistry(EntityType,FieldKey,DisplayName,CandidateOffset,DataType,ValidationStatus,Evidence,LastVerifiedGameVersion,LastVerifiedModuleSha256) VALUES($entity,$key,$display,$offset,$type,$status,$evidence,$version,$hash) ON CONFLICT(EntityType,FieldKey) DO UPDATE SET DisplayName=excluded.DisplayName,CandidateOffset=excluded.CandidateOffset,DataType=excluded.DataType,ValidationStatus=excluded.ValidationStatus,Evidence=excluded.Evidence,LastVerifiedGameVersion=excluded.LastVerifiedGameVersion,LastVerifiedModuleSha256=excluded.LastVerifiedModuleSha256;";
            reg.Parameters.AddWithValue("$entity", field.Item1); reg.Parameters.AddWithValue("$key", field.Item2); reg.Parameters.AddWithValue("$display", field.Item3);
            reg.Parameters.AddWithValue("$offset", field.Item4); reg.Parameters.AddWithValue("$type", field.Item5); reg.Parameters.AddWithValue("$status", field.Item6);
            reg.Parameters.AddWithValue("$evidence", field.Item7); reg.Parameters.AddWithValue("$version", payload.GameVersion); reg.Parameters.AddWithValue("$hash", payload.ModuleSha256);
            await reg.ExecuteNonQueryAsync();
        }
        var finish = db.CreateCommand(); finish.Transaction = (SqliteTransaction)tx;
        finish.CommandText = "UPDATE LiveImportRuns SET CompletedUtc=$utc,Status='Completed',PlayersImported=$p,ClubsImported=$c,ErrorSummary=$summary WHERE RunId=$id;";
        finish.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O")); finish.Parameters.AddWithValue("$p", payload.Players.Count);
        finish.Parameters.AddWithValue("$c", payload.Clubs.Count); finish.Parameters.AddWithValue("$summary", $"Unnamed/unreadable player slots: {payload.UnnamedPlayers}; unresolved player-to-club links: {payload.UnlinkedPlayers}; imported player attribute/personality values: {liveAttributeCount}; CA/PA and attribute offsets remain provisional pending in-game UI witnesses"); finish.Parameters.AddWithValue("$id", runId);
        await finish.ExecuteNonQueryAsync(); await tx.CommitAsync().ConfigureAwait(false);
        return new LiveImportResult(runId, payload.PlayerSlots, payload.Players.Count, payload.Clubs.Count, payload.Clubs.Count, payload.UnnamedPlayers, payload.UnlinkedPlayers, liveAttributeCount);
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task<List<PlayerRecord>> SearchLivePlayersAsync(string? search, int limit = 5000, string? attributeName = null, double? minAttribute = null, double? maxAttribute = null, int? minAge = null, int? maxAge = null, DateTime? ageAsOf = null, string regenFilter = "Either", int offset = 0,
        int? minCa = null, int? maxCa = null, int? minPa = null, int? maxPa = null)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT p.PlayerUid, p.PlayerName, c.ClubName, p.NameValidation, p.RelationshipValidation,
                   p.DateOfBirth, p.RegenStatus, p.RegenConfidence,
                   (SELECT f.NumericValue FROM LivePlayerFacts f WHERE f.PlayerUid=p.PlayerUid AND lower(f.FieldKey) IN ('ability.current ability','ability.current ability rating') AND f.NumericValue IS NOT NULL AND f.ValidationStatus LIKE '%Verified%' LIMIT 1),
                   (SELECT f.NumericValue FROM LivePlayerFacts f WHERE f.PlayerUid=p.PlayerUid AND lower(f.FieldKey) IN ('ability.potential ability','ability.potential ability rating') AND f.NumericValue IS NOT NULL AND f.ValidationStatus LIKE '%Verified%' LIMIT 1)
            FROM LivePlayers p LEFT JOIN LiveClubs c ON c.ClubUid=p.ClubUid
            WHERE ($q='' OR p.PlayerName LIKE $pattern OR c.ClubName LIKE $pattern OR CAST(p.PlayerUid AS TEXT) LIKE $pattern)
              AND ($attribute='' OR EXISTS (SELECT 1 FROM LivePlayerFacts f WHERE f.PlayerUid=p.PlayerUid AND substr(f.FieldKey,instr(f.FieldKey,'.')+1)=$attribute AND f.ValidationStatus LIKE '%Verified%' AND ($min IS NULL OR f.NumericValue >= $min) AND ($max IS NULL OR f.NumericValue <= $max)))
              AND ($minCa IS NULL OR (SELECT f.NumericValue FROM LivePlayerFacts f WHERE f.PlayerUid=p.PlayerUid AND lower(f.FieldKey) IN ('ability.current ability','ability.current ability rating') AND f.ValidationStatus LIKE '%Verified%' LIMIT 1) >= $minCa)
              AND ($maxCa IS NULL OR (SELECT f.NumericValue FROM LivePlayerFacts f WHERE f.PlayerUid=p.PlayerUid AND lower(f.FieldKey) IN ('ability.current ability','ability.current ability rating') AND f.ValidationStatus LIKE '%Verified%' LIMIT 1) <= $maxCa)
              AND ($minPa IS NULL OR (SELECT f.NumericValue FROM LivePlayerFacts f WHERE f.PlayerUid=p.PlayerUid AND lower(f.FieldKey) IN ('ability.potential ability','ability.potential ability rating') AND f.ValidationStatus LIKE '%Verified%' LIMIT 1) >= $minPa)
              AND ($maxPa IS NULL OR (SELECT f.NumericValue FROM LivePlayerFacts f WHERE f.PlayerUid=p.PlayerUid AND lower(f.FieldKey) IN ('ability.potential ability','ability.potential ability rating') AND f.ValidationStatus LIKE '%Verified%' LIMIT 1) <= $maxPa)
              AND ($minAge IS NULL OR (p.DateOfBirth IS NOT NULL AND CAST(strftime('%Y',$asOf) AS INTEGER)-CAST(substr(p.DateOfBirth,1,4) AS INTEGER)-CASE WHEN strftime('%m-%d',$asOf) < substr(p.DateOfBirth,6,5) THEN 1 ELSE 0 END >= $minAge))
              AND ($maxAge IS NULL OR (p.DateOfBirth IS NOT NULL AND CAST(strftime('%Y',$asOf) AS INTEGER)-CAST(substr(p.DateOfBirth,1,4) AS INTEGER)-CASE WHEN strftime('%m-%d',$asOf) < substr(p.DateOfBirth,6,5) THEN 1 ELSE 0 END <= $maxAge))
              AND ($regen='Either' OR p.RegenStatus=$regen)
            ORDER BY CASE WHEN $q <> '' AND p.PlayerName LIKE $prefix THEN 0 ELSE 1 END, p.PlayerName COLLATE NOCASE LIMIT $limit OFFSET $offset;
            """;
        var q = (search ?? string.Empty).Trim();
        cmd.Parameters.AddWithValue("$q", q);
        cmd.Parameters.AddWithValue("$pattern", $"%{q}%");
        cmd.Parameters.AddWithValue("$prefix", $"{q}%");
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100000));
        cmd.Parameters.AddWithValue("$offset", Math.Max(0, offset));
        cmd.Parameters.AddWithValue("$attribute", attributeName ?? "");
        cmd.Parameters.AddWithValue("$min", (object?)minAttribute ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$max", (object?)maxAttribute ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$minAge", (object?)minAge ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$maxAge", (object?)maxAge ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$asOf", (ageAsOf ?? DateTime.Today).ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$regen", regenFilter);
        cmd.Parameters.AddWithValue("$minCa", (object?)minCa ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$maxCa", (object?)maxCa ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$minPa", (object?)minPa ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$maxPa", (object?)maxPa ?? DBNull.Value);
        var rows = new List<PlayerRecord>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            rows.Add(new PlayerRecord
            {
                SourceId = $"LIVE:{reader.GetInt64(0)}",
                Name = reader.GetString(1),
                DateOfBirth = reader.IsDBNull(5) || !DateTime.TryParse(reader.GetString(5), out var parsedDob) ? null : parsedDob,
                Age = reader.IsDBNull(5) || !DateTime.TryParse(reader.GetString(5), out var parsedAgeDob) ? null : CalculateAge(parsedAgeDob, ageAsOf ?? DateTime.Today),
                RegenStatus = reader.IsDBNull(6) ? "Unknown" : reader.GetString(6),
                RegenConfidence = reader.IsDBNull(7) ? "Unverified" : reader.GetString(7),
                Club = reader.IsDBNull(2) ? null : reader.GetString(2),
                CurrentAbility = reader.IsDBNull(8) ? null : Convert.ToInt32(reader.GetDouble(8)),
                PotentialAbility = reader.IsDBNull(9) ? null : Convert.ToInt32(reader.GetDouble(9)),
                DataOrigin = $"FM27 Live · {reader.GetString(3)} · {reader.GetString(4)}"
            });
        }
        return rows;
    }

    private static int CalculateAge(DateTime dob, DateTime asOf)
    {
        var age = asOf.Year - dob.Year;
        if (dob.Date > asOf.Date.AddYears(-age)) age--;
        return Math.Max(0, age);
    }

    public async Task<int> CountLocalPlayersAsync(string? search = null)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Players WHERE ($q='' OR Name LIKE $p OR COALESCE(Club,'') LIKE $p OR COALESCE(Nation,'') LIKE $p) AND (SourceId NOT LIKE 'FMSAVE:%' OR EXISTS (SELECT 1 FROM FmsavePlayerSnapshots s WHERE s.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) AND s.PlayerSourceId=Players.SourceId));";
        var q = (search ?? string.Empty).Trim();
        cmd.Parameters.AddWithValue("$q", q);
        cmd.Parameters.AddWithValue("$p", $"%{q}%");
        return Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
    }

    public async Task<List<PlayerRecord>> GetPlayersForClubAsync(string clubSourceId, bool liveSource = false, int limit = 500)
    {
        if (liveSource && long.TryParse(clubSourceId, out var liveUid))
            return await GetLivePlayersForClubAsync(liveUid, limit);

        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT SourceId,Name,Age,Club,Nation,League,Position,CurrentAbility,PotentialAbility,Value,HomeGrownClub,HomeGrownNation,DataOrigin,PreferredFoot,LeftFoot,RightFoot FROM Players WHERE SaveClubUid=$club AND (SourceId NOT LIKE 'FMSAVE:%' OR EXISTS (SELECT 1 FROM FmsavePlayerSnapshots s WHERE s.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) AND s.PlayerSourceId=Players.SourceId)) ORDER BY Name COLLATE NOCASE LIMIT $limit;";
        cmd.Parameters.AddWithValue("$club", clubSourceId);
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));
        var rows = new List<PlayerRecord>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            rows.Add(new PlayerRecord {
                SourceId = reader.GetString(0), Name = reader.GetString(1),
                Age = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Club = reader.IsDBNull(3) ? null : reader.GetString(3),
                Nation = reader.IsDBNull(4) ? null : reader.GetString(4),
                League = reader.IsDBNull(5) ? null : reader.GetString(5),
                Position = reader.IsDBNull(6) ? null : reader.GetString(6),
                CurrentAbility = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                PotentialAbility = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Value = reader.IsDBNull(9) ? null : reader.GetDecimal(9),
                HomeGrownClub = reader.IsDBNull(10) ? null : reader.GetInt32(10) == 1,
                HomeGrownNation = reader.IsDBNull(11) ? null : reader.GetInt32(11) == 1,
                DataOrigin = reader.GetString(12),
                PreferredFoot = reader.IsDBNull(13) ? null : reader.GetString(13),
                LeftFoot = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                RightFoot = reader.IsDBNull(15) ? null : reader.GetInt32(15)
            });
        }
        return rows;
    }

    public async Task<int> CountLivePlayersAsync(string? search = null)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM LivePlayers p LEFT JOIN LiveClubs c ON c.ClubUid=p.ClubUid WHERE ($q='' OR p.PlayerName LIKE $pattern OR c.ClubName LIKE $pattern OR CAST(p.PlayerUid AS TEXT) LIKE $pattern);";
        var q = (search ?? string.Empty).Trim();
        cmd.Parameters.AddWithValue("$q", q);
        cmd.Parameters.AddWithValue("$pattern", $"%{q}%");
        return Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
    }

    public async Task<List<LiveClubSummary>> SearchLiveClubsAsync(string? search, int limit = 5000)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT c.ClubUid, c.ClubName, c.ValidationStatus, COUNT(p.PlayerUid)
            FROM LiveClubs c LEFT JOIN LivePlayers p ON p.ClubUid=c.ClubUid
            WHERE ($q='' OR c.ClubName LIKE $pattern OR CAST(c.ClubUid AS TEXT) LIKE $pattern)
            GROUP BY c.ClubUid,c.ClubName,c.ValidationStatus
            ORDER BY c.ClubName COLLATE NOCASE LIMIT $limit;
            """;
        var q = (search ?? string.Empty).Trim();
        cmd.Parameters.AddWithValue("$q", q);
        cmd.Parameters.AddWithValue("$pattern", $"%{q}%");
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100000));
        var rows = new List<LiveClubSummary>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false)) rows.Add(new LiveClubSummary(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
        return rows;
    }

    public async Task<int> CountLiveClubsAsync(string? search = null)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM LiveClubs WHERE ($q='' OR ClubName LIKE $pattern OR CAST(ClubUid AS TEXT) LIKE $pattern);";
        var q = (search ?? string.Empty).Trim();
        cmd.Parameters.AddWithValue("$q", q);
        cmd.Parameters.AddWithValue("$pattern", $"%{q}%");
        return Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
    }

    public async Task<List<PlayerRecord>> GetLivePlayersForClubAsync(long clubUid, int limit = 5000)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT p.PlayerUid,p.PlayerName,c.ClubName,p.DateOfBirth,
              (SELECT f.NumericValue FROM LivePlayerFacts f WHERE f.PlayerUid=p.PlayerUid AND lower(f.FieldKey) IN ('ability.current ability','ability.current ability rating') AND f.ValidationStatus LIKE '%Verified%' LIMIT 1),
              (SELECT f.NumericValue FROM LivePlayerFacts f WHERE f.PlayerUid=p.PlayerUid AND lower(f.FieldKey) IN ('ability.potential ability','ability.potential ability rating') AND f.ValidationStatus LIKE '%Verified%' LIMIT 1)
            FROM LivePlayers p LEFT JOIN LiveClubs c ON c.ClubUid=p.ClubUid
            WHERE p.ClubUid=$club ORDER BY p.PlayerName COLLATE NOCASE LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$club", clubUid);
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100000));
        var rows = new List<PlayerRecord>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            DateTime? dob = reader.IsDBNull(3) || !DateTime.TryParse(reader.GetString(3), out var parsedDob) ? null : parsedDob;
            rows.Add(new PlayerRecord
            {
                SourceId = $"LIVE:{reader.GetInt64(0)}", Name = reader.GetString(1),
                Club = reader.IsDBNull(2) ? null : reader.GetString(2), DateOfBirth = dob,
                Age = dob.HasValue ? CalculateAge(dob.Value, DateTime.Today) : null,
                CurrentAbility = reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetDouble(4)),
                PotentialAbility = reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetDouble(5)),
                DataOrigin = "FM27 Live"
            });
        }
        return rows;
    }

    public async Task<(int Players, int Clubs, int Staff)> GetLiveDatabaseCountsAsync()
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        async Task<int> Count(string table)
        {
            var cmd = db.CreateCommand(); cmd.CommandText = $"SELECT COUNT(*) FROM {table};";
            return Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
        }
        return (await Count("LivePlayers"), await Count("LiveClubs"), await Count("LiveStaff"));
    }



    public async Task<FmsaveImportResult> ImportFmsaveSnapshotAsync(string jsonPath, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        await WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        var isBundle = Directory.Exists(jsonPath);
        var manifestPath = isBundle ? Path.Combine(jsonPath, "manifest.json") : jsonPath;
        if (!File.Exists(manifestPath)) throw new FileNotFoundException("The FM Atlas save snapshot manifest is missing.", manifestPath);
        await using var documentStream = File.OpenRead(manifestPath);
        using var document = await JsonDocument.ParseAsync(documentStream, new JsonDocumentOptions { MaxDepth = 128 }, cancellationToken);
        var root = document.RootElement;
        var schema = GetString(root, "schema");
        if (isBundle ? schema != "fmatlas.fmsave.snapshot.ndjson.v1" : schema != "fmatlas.fmsave.snapshot.v1")
            throw new InvalidDataException("This is not a supported FM Atlas save snapshot or NDJSON bundle. Re-export the save using the bundled fmsave bridge.");
        var playerTotal = isBundle ? GetManifestTableCount(root, "players") : GetArrayCount(root, "players");
        var staffTotal = isBundle ? GetManifestTableCount(root, "staff") : GetArrayCount(root, "staff");
        var clubTotal = isBundle ? GetManifestTableCount(root, "clubs") : GetArrayCount(root, "clubs");
        if (playerTotal == 0) throw new InvalidDataException("The save reader returned no player records. No database changes were made.");
        var warnings = GetArray(root, "warnings").Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : x.GetRawText()).Where(x => x.Length > 0).ToList();
        var tableCounts = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("counts", out var countObject) && countObject.ValueKind == JsonValueKind.Object)
            foreach (var property in countObject.EnumerateObject()) tableCounts[property.Name] = property.Value.TryGetInt32(out var n) ? n : 0;
        var sourceName = GetString(root, "source_file_name") ?? Path.GetFileName(jsonPath);
        var sourceSize = GetLong(root, "source_file_size") ?? 0;
        var adapterVersion = GetString(root, "fmsave_version");
        var saveInfo = root.TryGetProperty("save_info", out var saveInfoElement) && saveInfoElement.ValueKind == JsonValueKind.Object ? saveInfoElement : default;
        var validation = root.TryGetProperty("validation", out var validationElement) && validationElement.ValueKind == JsonValueKind.Object ? validationElement : default;
        var validationJson = validation.ValueKind == JsonValueKind.Object ? validation.GetRawText() : null;
        var game = GetString(saveInfo, "game");
        var gameBuild = GetString(saveInfo, "build");
        var knownBuild = saveInfo.ValueKind == JsonValueKind.Object && saveInfo.TryGetProperty("known_build", out var knownElement) && knownElement.ValueKind is JsonValueKind.True or JsonValueKind.False ? knownElement.GetBoolean() : (bool?)null;
        var gameDate = GetString(saveInfo, "game_date");
        if (knownBuild == false) warnings.Add("The save build is not registered by fmsave; reader layouts are unverified for this build.");
        if (string.Equals(game, "FM27", StringComparison.OrdinalIgnoreCase)) warnings.Add("FM27 field meanings remain unconfirmed by fmsave, even on a registered build. Validate selected values against the in-game UI.");

        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        await using var tx = await db.BeginTransactionAsync();
        var now = DateTime.UtcNow.ToString("O");
        var run = db.CreateCommand(); run.Transaction = (SqliteTransaction)tx;
        run.CommandText = "INSERT INTO FmsaveImportRuns(SourceFileName,SourceFileSize,AdapterVersion,CreatedUtc,WarningsJson,TableCountsJson,Game,GameBuild,KnownBuild,GameDate,ValidationJson) VALUES($name,$size,$version,$utc,$warnings,$counts,$game,$build,$known,$date,$validation); SELECT last_insert_rowid();";
        run.Parameters.AddWithValue("$name", sourceName); run.Parameters.AddWithValue("$size", sourceSize);
        run.Parameters.AddWithValue("$version", (object?)adapterVersion ?? DBNull.Value); run.Parameters.AddWithValue("$utc", now);
        run.Parameters.AddWithValue("$warnings", JsonSerializer.Serialize(warnings)); run.Parameters.AddWithValue("$counts", JsonSerializer.Serialize(tableCounts));
        run.Parameters.AddWithValue("$game", (object?)game ?? DBNull.Value); run.Parameters.AddWithValue("$build", (object?)gameBuild ?? DBNull.Value);
        run.Parameters.AddWithValue("$known", knownBuild.HasValue ? (object)(knownBuild.Value ? 1 : 0) : DBNull.Value);
        run.Parameters.AddWithValue("$date", (object?)gameDate ?? DBNull.Value); run.Parameters.AddWithValue("$validation", (object?)validationJson ?? DBNull.Value);
        var snapshotId = Convert.ToInt32(await run.ExecuteScalarAsync());

        var playerUpsert = db.CreateCommand(); playerUpsert.Transaction = (SqliteTransaction)tx;
        playerUpsert.CommandText = "INSERT INTO Players(SourceId,Name,Age,Club,Nation,League,Position,CurrentAbility,PotentialAbility,Value,PreferredFoot,LeftFoot,RightFoot,SaveClubUid,DataOrigin,UpdatedUtc) VALUES($id,$name,$age,$club,$nation,$league,$position,$ca,$pa,$value,$foot,$left,$right,$clubUid,'FMSave snapshot',$utc) ON CONFLICT(SourceId) DO UPDATE SET Name=excluded.Name,Age=excluded.Age,Club=excluded.Club,Nation=excluded.Nation,League=excluded.League,Position=excluded.Position,CurrentAbility=excluded.CurrentAbility,PotentialAbility=excluded.PotentialAbility,Value=excluded.Value,PreferredFoot=excluded.PreferredFoot,LeftFoot=excluded.LeftFoot,RightFoot=excluded.RightFoot,SaveClubUid=excluded.SaveClubUid,DataOrigin=excluded.DataOrigin,UpdatedUtc=excluded.UpdatedUtc;";
        foreach (var key in new[] {"$id","$name","$age","$club","$nation","$league","$position","$ca","$pa","$value","$foot","$left","$right","$clubUid","$utc"}) playerUpsert.Parameters.Add(new SqliteParameter(key, DBNull.Value));
        var rawPlayer = db.CreateCommand(); rawPlayer.Transaction = (SqliteTransaction)tx;
        rawPlayer.CommandText = "INSERT OR REPLACE INTO FmsavePlayerSnapshots(SnapshotId,PlayerUid,PlayerSourceId,RawJson) VALUES($snapshot,$uid,$source,$raw);";
        rawPlayer.Parameters.AddWithValue("$snapshot", snapshotId); rawPlayer.Parameters.Add("$uid", SqliteType.Text); rawPlayer.Parameters.Add("$source", SqliteType.Text); rawPlayer.Parameters.Add("$raw", SqliteType.Text);
        var playerFactInsert = db.CreateCommand(); playerFactInsert.Transaction = (SqliteTransaction)tx;
        playerFactInsert.CommandText = "INSERT OR REPLACE INTO FmsavePlayerFacts(SnapshotId,PlayerSourceId,FactGroup,FieldKey,NumericValue,TextValue) VALUES($snapshot,$source,$group,$field,$number,$text);";
        playerFactInsert.Parameters.AddWithValue("$snapshot", snapshotId); playerFactInsert.Parameters.Add("$source", SqliteType.Text); playerFactInsert.Parameters.Add("$group", SqliteType.Text); playerFactInsert.Parameters.Add("$field", SqliteType.Text); playerFactInsert.Parameters.Add("$number", SqliteType.Real); playerFactInsert.Parameters.Add("$text", SqliteType.Text);
        var clearSnapshotAttributes = db.CreateCommand(); clearSnapshotAttributes.Transaction = (SqliteTransaction)tx;
        clearSnapshotAttributes.CommandText = "DELETE FROM PlayerAttributes WHERE SourceId=$id AND (Category LIKE 'attributes:%' OR Category='personality');";
        clearSnapshotAttributes.Parameters.Add("$id", SqliteType.Text);
        var attributeInsert = db.CreateCommand(); attributeInsert.Transaction = (SqliteTransaction)tx;
        attributeInsert.CommandText = "INSERT INTO PlayerAttributes(SourceId,AttributeName,Category,AttributeValue,UpdatedUtc) VALUES($id,$name,$category,$value,$utc) ON CONFLICT(SourceId,AttributeName) DO UPDATE SET Category=excluded.Category,AttributeValue=excluded.AttributeValue,UpdatedUtc=excluded.UpdatedUtc;";
        attributeInsert.Parameters.Add("$id", SqliteType.Text); attributeInsert.Parameters.Add("$name", SqliteType.Text); attributeInsert.Parameters.Add("$category", SqliteType.Text); attributeInsert.Parameters.Add("$value", SqliteType.Real); attributeInsert.Parameters.Add("$utc", SqliteType.Text);
        playerUpsert.Prepare(); rawPlayer.Prepare(); playerFactInsert.Prepare(); clearSnapshotAttributes.Prepare(); attributeInsert.Prepare();
        var playerCount = 0;
        var scannedPlayers = 0;
        await foreach (var player in ReadSnapshotRowsAsync(jsonPath, root, isBundle, "players", cancellationToken).ConfigureAwait(false))
        {
            if ((scannedPlayers++ % 250) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Indexing player records: {scannedPlayers:N0} / {playerTotal:N0}");
            }
            if (player.ValueKind != JsonValueKind.Object) continue;
            var saveUid = GetString(player, "uid") ?? GetString(player, "id");
            var persistentUid = GetString(player, "unique_id") ?? saveUid;
            var name = GetString(player, "name");
            if (string.IsNullOrWhiteSpace(saveUid) || string.IsNullOrWhiteSpace(persistentUid) || string.IsNullOrWhiteSpace(name)) continue;
            var sourceId = GetString(player, "atlas_source_id") ?? "FMSAVE:" + persistentUid;
            SetParameter(playerUpsert, "$id", sourceId); SetParameter(playerUpsert, "$name", name);
            SetParameter(playerUpsert, "$age", GetLong(player, "age")); SetParameter(playerUpsert, "$club", GetString(player, "club_name"));
            SetParameter(playerUpsert, "$nation", GetString(player, "nationality")); SetParameter(playerUpsert, "$league", GetString(player, "league"));
            SetParameter(playerUpsert, "$position", GetDisplayValue(player, "position"));
            SetParameter(playerUpsert, "$ca", GetLong(player, "current_ability")); SetParameter(playerUpsert, "$pa", GetLong(player, "potential_ability"));
            SetParameter(playerUpsert, "$value", GetNumeric(player, "value")); SetParameter(playerUpsert, "$foot", GetString(player, "preferred_foot"));
            SetParameter(playerUpsert, "$left", GetLong(player, "left_foot")); SetParameter(playerUpsert, "$right", GetLong(player, "right_foot"));
            SetParameter(playerUpsert, "$clubUid", GetString(player, "club_uid"));
            SetParameter(playerUpsert, "$utc", now);
            await playerUpsert.ExecuteNonQueryAsync();
            // Replace values for this external player key with the latest snapshot values so stale fields do not survive a later import.
            clearSnapshotAttributes.Parameters["$id"].Value = sourceId;
            await clearSnapshotAttributes.ExecuteNonQueryAsync();
            rawPlayer.Parameters["$uid"].Value = saveUid; rawPlayer.Parameters["$source"].Value = sourceId; rawPlayer.Parameters["$raw"].Value = player.GetRawText(); await rawPlayer.ExecuteNonQueryAsync();

            foreach (var group in new[] { "attributes", "personality" })
            {
                if (!player.TryGetProperty(group, out var groupElement) || groupElement.ValueKind != JsonValueKind.Object) continue;
                foreach (var fact in FlattenNumeric(groupElement))
                {
                    if (fact.Value < 1 || fact.Value > 20) continue;
                    var separator = fact.Key.LastIndexOf('.');
                    attributeInsert.Parameters["$id"].Value = sourceId;
                    attributeInsert.Parameters["$name"].Value = separator >= 0 ? fact.Key[(separator + 1)..] : fact.Key;
                    attributeInsert.Parameters["$category"].Value = separator >= 0 ? group + ":" + fact.Key[..separator] : group;
                    attributeInsert.Parameters["$value"].Value = fact.Value;
                    attributeInsert.Parameters["$utc"].Value = now;
                    await attributeInsert.ExecuteNonQueryAsync();
                }
            }
            foreach (var group in new[] { "contract", "traits", "positions", "natural_positions", "accomplished_positions", "general", "state", "reputation", "raw_attributes" })
            {
                if (!player.TryGetProperty(group, out var factsElement)) continue;
                foreach (var fact in FlattenScalarFacts(factsElement))
                {
                    playerFactInsert.Parameters["$source"].Value = sourceId;
                    playerFactInsert.Parameters["$group"].Value = group;
                    playerFactInsert.Parameters["$field"].Value = fact.Key;
                    playerFactInsert.Parameters["$number"].Value = fact.NumericValue.HasValue ? fact.NumericValue.Value : DBNull.Value;
                    playerFactInsert.Parameters["$text"].Value = fact.TextValue is null ? DBNull.Value : fact.TextValue;
                    await playerFactInsert.ExecuteNonQueryAsync();
                }
            }
            playerCount++;
        }

        var staffInsert = db.CreateCommand(); staffInsert.Transaction = (SqliteTransaction)tx;
        staffInsert.CommandText = "INSERT OR REPLACE INTO FmsaveStaffDirectory(SnapshotId,StaffUid,Name,ClubName,JobTitle,Age,Nation,RawJson) VALUES($snapshot,$uid,$name,$club,$job,$age,$nation,$raw);";
        staffInsert.Parameters.AddWithValue("$snapshot", snapshotId); staffInsert.Parameters.Add("$uid", SqliteType.Text); staffInsert.Parameters.Add("$name", SqliteType.Text); staffInsert.Parameters.Add("$club", SqliteType.Text); staffInsert.Parameters.Add("$job", SqliteType.Text); staffInsert.Parameters.Add("$age", SqliteType.Integer); staffInsert.Parameters.Add("$nation", SqliteType.Text); staffInsert.Parameters.Add("$raw", SqliteType.Text);
        staffInsert.Prepare();
        var staffCount = 0;
        var scannedStaff = 0;
        await foreach (var item in ReadSnapshotRowsAsync(jsonPath, root, isBundle, "staff", cancellationToken).ConfigureAwait(false))
        {
            if ((scannedStaff++ % 250) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Indexing staff records: {scannedStaff:N0} / {staffTotal:N0}");
            }
            var uid = GetString(item, "uid") ?? GetString(item, "unique_id") ?? GetString(item, "id");
            var name = GetString(item, "name"); if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(name)) continue;
            staffInsert.Parameters["$uid"].Value = uid; staffInsert.Parameters["$name"].Value = name;
            SetNullable(staffInsert.Parameters["$club"], GetString(item, "club_name")); SetNullable(staffInsert.Parameters["$job"], GetString(item, "job_title") ?? GetString(item, "job"));
            SetNullable(staffInsert.Parameters["$age"], GetLong(item, "age")); SetNullable(staffInsert.Parameters["$nation"], GetString(item, "nationality"));
            staffInsert.Parameters["$raw"].Value = item.GetRawText(); await staffInsert.ExecuteNonQueryAsync(); staffCount++;
        }

        var clubInsert = db.CreateCommand(); clubInsert.Transaction = (SqliteTransaction)tx;
        clubInsert.CommandText = "INSERT OR REPLACE INTO FmsaveClubDirectory(SnapshotId,ClubUid,Name,Nation,RawJson) VALUES($snapshot,$uid,$name,$nation,$raw);";
        clubInsert.Parameters.AddWithValue("$snapshot", snapshotId); clubInsert.Parameters.Add("$uid", SqliteType.Text); clubInsert.Parameters.Add("$name", SqliteType.Text); clubInsert.Parameters.Add("$nation", SqliteType.Text); clubInsert.Parameters.Add("$raw", SqliteType.Text);
        clubInsert.Prepare();
        var clubCount = 0;
        var scannedClubs = 0;
        await foreach (var item in ReadSnapshotRowsAsync(jsonPath, root, isBundle, "clubs", cancellationToken).ConfigureAwait(false))
        {
            if ((scannedClubs++ % 250) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Indexing club records: {scannedClubs:N0} / {clubTotal:N0}");
            }
            var uid = GetString(item, "uid") ?? GetString(item, "club_uid") ?? GetString(item, "unique_id") ?? GetString(item, "id");
            var name = GetString(item, "name") ?? GetString(item, "club_name"); if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(name)) continue;
            clubInsert.Parameters["$uid"].Value = uid; clubInsert.Parameters["$name"].Value = name;
            SetNullable(clubInsert.Parameters["$nation"], GetString(item, "nation_name") ?? GetString(item, "nation"));
            clubInsert.Parameters["$raw"].Value = item.GetRawText(); await clubInsert.ExecuteNonQueryAsync(); clubCount++;
        }

        // Keep all additional save tables in a lossless JSON sidecar table so later modules can build on them.
        var extraNames = new[] { "managed_clubs", "staff_lists", "contracts", "suspensions", "finances", "sponsorships", "facilities", "stadiums", "affiliates", "job_vacancies", "stages", "competitions", "nations", "injury_types", "injuries", "player_season_stats", "player_season_history", "player_match_stats", "fixtures", "league_tables", "competition_rules", "transfer_windows", "tactics", "training", "mentoring", "set_pieces", "selections" };
        var extraInsert = db.CreateCommand(); extraInsert.Transaction = (SqliteTransaction)tx;
        extraInsert.CommandText = "INSERT INTO FmsaveExtraTables(SnapshotId,TableName,RowIndex,RawJson) VALUES($snapshot,$table,$index,$raw);";
        extraInsert.Parameters.AddWithValue("$snapshot", snapshotId); extraInsert.Parameters.Add("$table", SqliteType.Text); extraInsert.Parameters.Add("$index", SqliteType.Integer); extraInsert.Parameters.Add("$raw", SqliteType.Text);
        extraInsert.Prepare();
        var statFactInsert = db.CreateCommand(); statFactInsert.Transaction = (SqliteTransaction)tx;
        statFactInsert.CommandText = "INSERT OR REPLACE INTO FmsaveStatFacts(SnapshotId,TableName,RowIndex,PlayerUid,PeriodKey,Minutes,FieldKey,NumericValue) VALUES($snapshot,$table,$index,$uid,$period,$minutes,$field,$value);";
        statFactInsert.Parameters.AddWithValue("$snapshot", snapshotId); statFactInsert.Parameters.Add("$table", SqliteType.Text); statFactInsert.Parameters.Add("$index", SqliteType.Integer);
        statFactInsert.Parameters.Add("$uid", SqliteType.Text); statFactInsert.Parameters.Add("$period", SqliteType.Text); statFactInsert.Parameters.Add("$minutes", SqliteType.Real);
        statFactInsert.Parameters.Add("$field", SqliteType.Text); statFactInsert.Parameters.Add("$value", SqliteType.Real); statFactInsert.Prepare();
        var extraCount = 0; var statFactCount = 0;
        foreach (var tableName in extraNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Indexing {tableName} records…");
            var rowIndex = 0;
            await foreach (var row in ReadSnapshotRowsAsync(jsonPath, root, isBundle, tableName, cancellationToken).ConfigureAwait(false))
            {
                var currentIndex = rowIndex++;
                if ((extraCount % 500) == 0) cancellationToken.ThrowIfCancellationRequested();
                var rawJson = row.GetRawText();
                extraInsert.Parameters["$table"].Value = tableName; extraInsert.Parameters["$index"].Value = currentIndex; extraInsert.Parameters["$raw"].Value = rawJson;
                await extraInsert.ExecuteNonQueryAsync(); extraCount++;
                if ((tableName is "player_season_stats" or "player_season_history" or "player_match_stats") && row.ValueKind == JsonValueKind.Object)
                {
                    var uid = GetFirstJsonString(row, "player_uid", "player_id", "uid", "unique_id", "person_uid");
                    if (!string.IsNullOrWhiteSpace(uid))
                    {
                        var period = tableName == "player_match_stats"
                            ? GetFirstJsonString(row, "fixture_uid", "match_uid", "fixture_id", "match_id", "date", "match_date") ?? $"Match #{currentIndex + 1}"
                            : GetFirstJsonString(row, "season", "season_name", "year") ?? "Season unavailable";
                        double? minutes = null;
                        foreach (var key in new[] { "minutes_played", "playing_time_minutes", "minutes", "mins_played", "total_minutes", "minutes_on_pitch" })
                            if (TryFindNumeric(row, key, out var mins) && mins > 0) { minutes = mins; break; }
                        foreach (var metric in EnumerateNumericPaths(row))
                        {
                            var leaf = metric.Key.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? metric.Key;
                            if (IsAnalyticsIdentityKey(leaf) || IsNonMetricNumericField(leaf) || !double.IsFinite(metric.Value)) continue;
                            statFactInsert.Parameters["$table"].Value = tableName; statFactInsert.Parameters["$index"].Value = currentIndex;
                            statFactInsert.Parameters["$uid"].Value = uid; statFactInsert.Parameters["$period"].Value = period;
                            statFactInsert.Parameters["$minutes"].Value = (object?)minutes ?? DBNull.Value;
                            statFactInsert.Parameters["$field"].Value = metric.Key; statFactInsert.Parameters["$value"].Value = metric.Value;
                            await statFactInsert.ExecuteNonQueryAsync().ConfigureAwait(false); statFactCount++;
                        }
                    }
                }
            }
        }
        if (playerCount == 0)
            throw new InvalidDataException("The save bundle contained player rows, but none could be indexed. The previous committed career snapshot was retained.");

        if (playerCount != playerTotal)
            warnings.Add($"Player row reconciliation: manifest={playerTotal:N0}, indexed={playerCount:N0}. Invalid or incomplete player rows were skipped.");
        if (staffCount != staffTotal)
            warnings.Add($"Staff row reconciliation: manifest={staffTotal:N0}, indexed={staffCount:N0}. Rows without a usable staff identity/name were skipped.");
        if (clubCount != clubTotal)
            warnings.Add($"Club row reconciliation: manifest={clubTotal:N0}, indexed={clubCount:N0}. Rows without a usable club identity/name were skipped.");

        var expectedExtraRows = tableCounts
            .Where(x => !x.Key.Equals("players", StringComparison.OrdinalIgnoreCase)
                && !x.Key.Equals("staff", StringComparison.OrdinalIgnoreCase)
                && !x.Key.Equals("clubs", StringComparison.OrdinalIgnoreCase))
            .Sum(x => (long)x.Value);
        if (expectedExtraRows != extraCount)
            warnings.Add($"Additional table row reconciliation: manifest={expectedExtraRows:N0}, indexed={extraCount:N0}. Check the bridge bundle before relying on full table coverage.");

        var finish = db.CreateCommand(); finish.Transaction = (SqliteTransaction)tx;
        finish.CommandText = "UPDATE FmsaveImportRuns SET PlayersImported=$players,StaffImported=$staff,ClubsImported=$clubs,ExtraRowsImported=$extra,WarningsJson=$warnings WHERE SnapshotId=$id;";
        finish.Parameters.AddWithValue("$players", playerCount); finish.Parameters.AddWithValue("$staff", staffCount); finish.Parameters.AddWithValue("$clubs", clubCount); finish.Parameters.AddWithValue("$extra", extraCount);
        finish.Parameters.AddWithValue("$warnings", JsonSerializer.Serialize(warnings)); finish.Parameters.AddWithValue("$id", snapshotId);
        await finish.ExecuteNonQueryAsync();
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Validating and committing the new career snapshot…");
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        AtlasLog.Info($"FMSave snapshot indexed: snapshot={snapshotId}; playerStatFacts={statFactCount}; rawExtraRows={extraCount}.");
        return new FmsaveImportResult(snapshotId, playerCount, staffCount, clubCount, extraCount, tableCounts, warnings);
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task<(int ImportId, int RowsImported, int PlayersCreated, string Note)> ImportFmViewExporterCsvAsync(string csvPath)
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        var lines = await File.ReadAllLinesAsync(csvPath, Encoding.UTF8).ConfigureAwait(false);
        if (lines.Length < 2) throw new InvalidDataException("The FM View Exporter CSV is empty or has no data rows.");
        var delimiter = lines[0].Count(c => c == ';') > lines[0].Count(c => c == ',') ? ';' : ',';
        var rawHeaders = ParseCsvLine(lines[0], delimiter).Select(x => x.Trim().TrimStart('\uFEFF')).ToArray();
        var headers = new string[rawHeaders.Length];
        var duplicateCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rawHeaders.Length; i++)
        {
            var header = string.IsNullOrWhiteSpace(rawHeaders[i]) ? $"Column_{i + 1}" : rawHeaders[i];
            duplicateCounts.TryGetValue(header, out var seen); seen++; duplicateCounts[header] = seen;
            headers[i] = seen == 1 ? header : header + "_" + seen;
        }
        var index = headers.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);
        int? Find(params string[] names) { foreach (var n in names) if (index.TryGetValue(n, out var i)) return i; return null; }
        string? Value(string[] row, params string[] names) { var i = Find(names); return i.HasValue && i.Value < row.Length && !string.IsNullOrWhiteSpace(row[i.Value]) ? row[i.Value].Trim() : null; }
        double? Number(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var cleaned = value.Replace("%", "").Replace("£", "").Replace("€", "").Replace("$", "").Trim();
            if (cleaned.Contains(',') && !cleaned.Contains('.')) cleaned = cleaned.Replace(',', '.');
            return double.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? d : null;
        }
        var idColumn = Find("Unique ID", "Unique ID_2", "Player ID", "ID");
        var nameColumn = Find("Player", "Name");
        if (!idColumn.HasValue || !nameColumn.HasValue) throw new InvalidDataException("The CSV needs the FM player 'Unique ID' and 'Player' columns. Export those two fields from the in-game view.");

        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        await using var tx = await db.BeginTransactionAsync().ConfigureAwait(false);
        var now = DateTime.UtcNow.ToString("O");
        var run = db.CreateCommand(); run.Transaction = (SqliteTransaction)tx;
        run.CommandText = "INSERT INTO ExternalStatImportRuns(FileName,ImportedUtc,RowsSeen,RowsImported,Notes) VALUES($file,$utc,$seen,0,'FM View Exporter CSV · source columns retained as imported'); SELECT last_insert_rowid();";
        run.Parameters.AddWithValue("$file", Path.GetFileName(csvPath)); run.Parameters.AddWithValue("$utc", now); run.Parameters.AddWithValue("$seen", lines.Length - 1);
        var importId = Convert.ToInt32(await run.ExecuteScalarAsync().ConfigureAwait(false));
        var existingIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mapCmd = db.CreateCommand(); mapCmd.Transaction = (SqliteTransaction)tx;
        mapCmd.CommandText = "SELECT PlayerUid,PlayerSourceId FROM FmsavePlayerSnapshots WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns);";
        await using (var mr = await mapCmd.ExecuteReaderAsync().ConfigureAwait(false))
            while (await mr.ReadAsync().ConfigureAwait(false)) if (!mr.IsDBNull(0) && !mr.IsDBNull(1)) existingIds[mr.GetString(0)] = mr.GetString(1);

        var playerUpsert = db.CreateCommand(); playerUpsert.Transaction = (SqliteTransaction)tx;
        playerUpsert.CommandText = "INSERT INTO Players(SourceId,Name,Age,Club,Nation,League,Position,CurrentAbility,PotentialAbility,Value,PreferredFoot,DataOrigin,UpdatedUtc) VALUES($id,$name,$age,$club,$nation,$league,$position,$ca,$pa,$value,$foot,'FM View Exporter CSV',$utc) ON CONFLICT(SourceId) DO UPDATE SET Name=excluded.Name,Age=COALESCE(excluded.Age,Players.Age),Club=COALESCE(excluded.Club,Players.Club),Nation=COALESCE(excluded.Nation,Players.Nation),League=COALESCE(excluded.League,Players.League),Position=COALESCE(excluded.Position,Players.Position),CurrentAbility=COALESCE(excluded.CurrentAbility,Players.CurrentAbility),PotentialAbility=COALESCE(excluded.PotentialAbility,Players.PotentialAbility),Value=COALESCE(excluded.Value,Players.Value),PreferredFoot=COALESCE(excluded.PreferredFoot,Players.PreferredFoot),DataOrigin='FM View Exporter CSV',UpdatedUtc=excluded.UpdatedUtc;";
        foreach (var pName in new[] { "$id", "$name", "$age", "$club", "$nation", "$league", "$position", "$ca", "$pa", "$value", "$foot", "$utc" }) playerUpsert.Parameters.Add(pName, SqliteType.Text);
        var rowInsert = db.CreateCommand(); rowInsert.Transaction = (SqliteTransaction)tx;
        rowInsert.CommandText = "INSERT INTO ExternalStatRows(ImportId,RowIndex,PlayerIdentity,PlayerSourceId,PlayerName,Club,Season,Minutes,RawJson) VALUES($import,$index,$identity,$source,$name,$club,$season,$minutes,$raw);";
        foreach (var pName in new[] { "$import", "$index", "$identity", "$source", "$name", "$club", "$season", "$minutes", "$raw" }) rowInsert.Parameters.Add(pName, SqliteType.Text);
        var attrInsert = db.CreateCommand(); attrInsert.Transaction = (SqliteTransaction)tx;
        attrInsert.CommandText = "INSERT INTO PlayerAttributes(SourceId,AttributeName,Category,AttributeValue,UpdatedUtc) VALUES($id,$name,$category,$value,$utc) ON CONFLICT(SourceId,AttributeName) DO UPDATE SET Category=excluded.Category,AttributeValue=excluded.AttributeValue,UpdatedUtc=excluded.UpdatedUtc;";
        foreach (var pName in new[] { "$id", "$name", "$category", "$value", "$utc" }) attrInsert.Parameters.Add(pName, SqliteType.Text);

        var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        for (var line = 1; line < lines.Length; line++)
        {
            if (string.IsNullOrWhiteSpace(lines[line])) continue;
            var row = ParseCsvLine(lines[line], delimiter);
            var identity = Value(row, "Unique ID", "Unique ID_2", "Player ID", "ID");
            var playerName = Value(row, "Player", "Name");
            if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(playerName)) continue;
            var sourceId = existingIds.TryGetValue(identity, out var existingSource) ? existingSource : "FMVIEW:" + identity;
            var club = Value(row, "Club"); var nation = Value(row, "Nation", "Nation_2"); var league = Value(row, "Division", "League");
            var position = Value(row, "Position", "Best Pos"); var age = Number(Value(row, "Age"));
            var ca = Number(Value(row, "CA", "Current Ability")); var pa = Number(Value(row, "PA", "Potential Ability"));
            var val = Number(Value(row, "Transfer Value")); var foot = Value(row, "Preferred Foot");
            playerUpsert.Parameters["$id"].Value = sourceId; playerUpsert.Parameters["$name"].Value = playerName;
            playerUpsert.Parameters["$age"].Value = (object?)age ?? DBNull.Value; playerUpsert.Parameters["$club"].Value = (object?)club ?? DBNull.Value;
            playerUpsert.Parameters["$nation"].Value = (object?)nation ?? DBNull.Value; playerUpsert.Parameters["$league"].Value = (object?)league ?? DBNull.Value;
            playerUpsert.Parameters["$position"].Value = (object?)position ?? DBNull.Value; playerUpsert.Parameters["$ca"].Value = (object?)ca ?? DBNull.Value;
            playerUpsert.Parameters["$pa"].Value = (object?)pa ?? DBNull.Value; playerUpsert.Parameters["$value"].Value = (object?)val ?? DBNull.Value;
            playerUpsert.Parameters["$foot"].Value = (object?)foot ?? DBNull.Value; playerUpsert.Parameters["$utc"].Value = now;
            await playerUpsert.ExecuteNonQueryAsync().ConfigureAwait(false); created.Add(sourceId);

            var rawObject = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["player_uid"] = identity, ["player_name"] = playerName, ["club"] = club,
                ["season"] = "FM View Export", ["minutes_played"] = Number(Value(row, "Minutes", "Mins"))
            };
            for (var i = 0; i < Math.Min(headers.Length, row.Length); i++)
            {
                var text = string.IsNullOrWhiteSpace(row[i]) ? null : row[i].Trim();
                if (text is null) continue;
                var numeric = Number(text);
                rawObject[headers[i]] = numeric.HasValue ? numeric.Value : text;
                var normalizedHeader = NormalizeAttributeKey(headers[i]);
                var attr = PlayerAttributeSchema.Fields.FirstOrDefault(x => NormalizeAttributeKey(x.Key) == normalizedHeader || NormalizeAttributeKey(x.Label) == normalizedHeader);
                if (attr is null) continue;
                if (numeric is >= 1 and <= 20)
                {
                    attrInsert.Parameters["$id"].Value = sourceId; attrInsert.Parameters["$name"].Value = attr.Key;
                    attrInsert.Parameters["$category"].Value = "attributes:" + attr.Category; attrInsert.Parameters["$value"].Value = numeric.Value;
                    attrInsert.Parameters["$utc"].Value = now; await attrInsert.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            var rawJson = JsonSerializer.Serialize(rawObject);
            var minutes = Number(Value(row, "Minutes", "Mins"));
            rowInsert.Parameters["$import"].Value = importId; rowInsert.Parameters["$index"].Value = line - 1;
            rowInsert.Parameters["$identity"].Value = identity; rowInsert.Parameters["$source"].Value = sourceId;
            rowInsert.Parameters["$name"].Value = playerName; rowInsert.Parameters["$club"].Value = (object?)club ?? DBNull.Value;
            rowInsert.Parameters["$season"].Value = "FM View Export"; rowInsert.Parameters["$minutes"].Value = (object?)minutes ?? DBNull.Value;
            rowInsert.Parameters["$raw"].Value = rawJson; await rowInsert.ExecuteNonQueryAsync().ConfigureAwait(false);
            imported++;
        }
        var finish = db.CreateCommand(); finish.Transaction = (SqliteTransaction)tx;
        finish.CommandText = "UPDATE ExternalStatImportRuns SET RowsImported=$rows WHERE ImportId=$id;";
        finish.Parameters.AddWithValue("$rows", imported); finish.Parameters.AddWithValue("$id", importId);
        await finish.ExecuteNonQueryAsync().ConfigureAwait(false);
        await tx.CommitAsync().ConfigureAwait(false);
        return (importId, imported, created.Count, $"{headers.Length} columns retained · source IDs matched to current FMSave players where possible · raw export rows preserved");
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task<List<AtlasEditableAttribute>> GetPlayerAttributeRowsAsync(string sourceId)
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand(); cmd.CommandText = "SELECT AttributeName,Category,AttributeValue FROM PlayerAttributes WHERE SourceId=$id ORDER BY Category,AttributeName;"; cmd.Parameters.AddWithValue("$id", sourceId);
        var rows = new List<AtlasEditableAttribute>(); await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false)) rows.Add(new AtlasEditableAttribute(reader.GetString(0),reader.GetString(1),reader.GetDouble(2)));
        return rows;
    }

    public async Task<List<AtlasFactRecord>> GetFmsavePlayerFactsAsync(string sourceId, string group)
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT FactGroup,FieldKey,COALESCE(CAST(NumericValue AS TEXT),TextValue) FROM FmsavePlayerFacts WHERE PlayerSourceId=$source AND FactGroup=$group AND SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) ORDER BY FieldKey;";
        cmd.Parameters.AddWithValue("$source", sourceId); cmd.Parameters.AddWithValue("$group", group);
        var rows = new List<AtlasFactRecord>(); await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false)) rows.Add(new AtlasFactRecord(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        return rows;
    }

    /// <summary>
    /// Returns every numeric season, history and match-stat field that the latest imported
    /// source actually contains for this player. This is deliberately lossless and
    /// provenance-preserving: fields are grouped by FM statistical family and period.
    /// </summary>
    public async Task<List<AtlasFactRecord>> GetPlayerPerformanceFactsAsync(string sourceId)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var snapshotCmd = db.CreateCommand();
        snapshotCmd.CommandText = "SELECT MAX(SnapshotId) FROM FmsaveImportRuns;";
        var snapshotValue = await snapshotCmd.ExecuteScalarAsync().ConfigureAwait(false);
        var snapshotId = snapshotValue is null || snapshotValue == DBNull.Value ? (int?)null : Convert.ToInt32(snapshotValue);
        var playerUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (snapshotId.HasValue)
        {
            var uidCmd = db.CreateCommand();
            uidCmd.CommandText = "SELECT PlayerUid FROM FmsavePlayerSnapshots WHERE SnapshotId=$snapshot AND PlayerSourceId=$source;";
            uidCmd.Parameters.AddWithValue("$snapshot", snapshotId.Value);
            uidCmd.Parameters.AddWithValue("$source", sourceId);
            await using var uidReader = await uidCmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await uidReader.ReadAsync().ConfigureAwait(false)) playerUids.Add(uidReader.GetString(0));
        }
        if (sourceId.StartsWith("LIVE:", StringComparison.OrdinalIgnoreCase) || sourceId.StartsWith("FMVIEW:", StringComparison.OrdinalIgnoreCase))
            playerUids.Add(sourceId[(sourceId.IndexOf(':') + 1)..]);

        var facts = new List<AtlasFactRecord>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var statFactCount = 0;
        if (snapshotId.HasValue && playerUids.Count > 0)
        {
            var count = db.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM FmsaveStatFacts WHERE SnapshotId=$snapshot;";
            count.Parameters.AddWithValue("$snapshot", snapshotId.Value);
            statFactCount = Convert.ToInt32(await count.ExecuteScalarAsync().ConfigureAwait(false));
        }

        // Preferred path: read the compact indexed metric facts. This avoids reparsing every
        // season/match JSON record whenever a player profile opens. Older snapshots fall back
        // to their original lossless JSON rows below.
        if (statFactCount > 0 && playerUids.Count > 0)
        {
            var parameters = new List<string>();
            var indexed = db.CreateCommand();
            indexed.Parameters.AddWithValue("$snapshot", snapshotId!.Value);
            var n = 0;
            foreach (var uid in playerUids)
            {
                var key = "$uid" + n++;
                parameters.Add(key);
                indexed.Parameters.AddWithValue(key, uid);
            }
            indexed.CommandText = $"SELECT TableName,PeriodKey,FieldKey,NumericValue FROM FmsaveStatFacts WHERE SnapshotId=$snapshot AND PlayerUid IN ({string.Join(",", parameters)}) ORDER BY TableName,RowIndex,FieldKey;";
            await using var reader = await indexed.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var table = reader.GetString(0);
                var period = reader.GetString(1);
                var key = reader.GetString(2);
                var value = reader.GetDouble(3);
                var leaf = key.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? key;
                if (IsAnalyticsIdentityKey(leaf) || IsNonMetricNumericField(leaf) || !double.IsFinite(value)) continue;
                var category = StatCategory(leaf, key);
                var fieldLabel = HumanizeMetric(leaf);
                var sourceLabel = table switch
                {
                    "player_season_stats" => "Season",
                    "player_season_history" => "Career history",
                    "player_match_stats" => "Match",
                    _ => table
                };
                var identity = category + "\0" + sourceLabel + "\0" + period + "\0" + fieldLabel;
                if (!seen.Add(identity)) continue;
                facts.Add(new AtlasFactRecord(category, $"{sourceLabel} · {period} · {fieldLabel}", value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
            }
        }
        else if (snapshotId.HasValue && playerUids.Count > 0)
        {
            var legacy = db.CreateCommand();
            legacy.CommandText = "SELECT TableName,RawJson FROM FmsaveExtraTables WHERE SnapshotId=$snapshot AND TableName IN ('player_season_stats','player_season_history','player_match_stats') ORDER BY CASE TableName WHEN 'player_season_stats' THEN 0 WHEN 'player_match_stats' THEN 1 ELSE 2 END,RowIndex;";
            legacy.Parameters.AddWithValue("$snapshot", snapshotId.Value);
            await using var reader = await legacy.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                try
                {
                    var table = reader.GetString(0);
                    using var doc = JsonDocument.Parse(reader.GetString(1));
                    var row = doc.RootElement;
                    var uid = GetFirstJsonString(row, "player_uid", "player_id", "uid", "unique_id", "person_uid");
                    if (string.IsNullOrWhiteSpace(uid) || !playerUids.Contains(uid)) continue;
                    var period = table == "player_match_stats"
                        ? GetFirstJsonString(row, "fixture_uid", "match_uid", "fixture_id", "match_id", "date", "match_date") ?? "Match period unavailable"
                        : GetFirstJsonString(row, "season", "season_name", "year") ?? "Season unavailable";
                    var sourceLabel = table switch { "player_season_stats" => "Season", "player_season_history" => "Career history", _ => "Match" };
                    foreach (var item in EnumerateNumericPaths(row))
                    {
                        var leaf = item.Key.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? item.Key;
                        if (IsAnalyticsIdentityKey(leaf) || IsNonMetricNumericField(leaf) || !double.IsFinite(item.Value)) continue;
                        var category = StatCategory(leaf, item.Key);
                        var fieldLabel = HumanizeMetric(leaf);
                        var identity = category + "\0" + sourceLabel + "\0" + period + "\0" + fieldLabel;
                        if (!seen.Add(identity)) continue;
                        facts.Add(new AtlasFactRecord(category, $"{sourceLabel} · {period} · {fieldLabel}", item.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                catch (JsonException) { /* An isolated malformed row must not suppress other profile stats. */ }
            }
        }

        // FM View Exporter data supplements save-reader stats. It is linked by person UID or
        // the Atlas source ID created during import; original column names are retained.
        var external = db.CreateCommand();
        external.CommandText = "SELECT PlayerIdentity,PlayerSourceId,RawJson FROM ExternalStatRows WHERE ImportId=(SELECT MAX(ImportId) FROM ExternalStatImportRuns) ORDER BY RowIndex;";
        await using (var reader = await external.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (!string.Equals(reader.GetString(1), sourceId, StringComparison.OrdinalIgnoreCase) && !playerUids.Contains(reader.GetString(0))) continue;
                try
                {
                    using var doc = JsonDocument.Parse(reader.GetString(2));
                    foreach (var item in EnumerateNumericPaths(doc.RootElement))
                    {
                        var leaf = item.Key.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? item.Key;
                        if (IsAnalyticsIdentityKey(leaf) || IsNonMetricNumericField(leaf) || !double.IsFinite(item.Value)) continue;
                        var category = StatCategory(leaf, item.Key);
                        var fieldLabel = HumanizeMetric(leaf);
                        var identity = category + "\0FM View Export\0" + fieldLabel;
                        if (!seen.Add(identity)) continue;
                        facts.Add(new AtlasFactRecord(category, $"FM View Export · {fieldLabel}", item.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                catch (JsonException) { }
            }
        }
        return facts.OrderBy(x => x.Group, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Field, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<WorkspaceMetrics> GetWorkspaceMetricsAsync()
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        async Task<int> Count(string sql) { var c=db.CreateCommand(); c.CommandText=sql; return Convert.ToInt32(await c.ExecuteScalarAsync() ?? 0); }
        async Task<string?> ScalarText(string sql) { var c=db.CreateCommand(); c.CommandText=sql; var v=await c.ExecuteScalarAsync(); return v is null || v==DBNull.Value ? null : Convert.ToString(v); }
        var knownBuildText = await ScalarText("SELECT CASE WHEN KnownBuild=1 THEN 'true' WHEN KnownBuild=0 THEN 'false' ELSE NULL END FROM FmsaveImportRuns ORDER BY SnapshotId DESC LIMIT 1");
        bool? knownBuild = bool.TryParse(knownBuildText, out var parsedKnownBuild) ? parsedKnownBuild : null;
        string? currentManager = null; string? currentManagerClub = null;
        var managerQuery = db.CreateCommand();
        managerQuery.CommandText = "SELECT RawJson FROM FmsaveStaffDirectory WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) ORDER BY Name LIMIT 10000;";
        await using (var managerReader = await managerQuery.ExecuteReaderAsync())
        {
            while (await managerReader.ReadAsync())
            {
                try
                {
                    using var managerDoc = JsonDocument.Parse(managerReader.GetString(0));
                    var manager = managerDoc.RootElement;
                    if (manager.TryGetProperty("is_human_manager", out var flag) && (flag.ValueKind == JsonValueKind.True || (flag.ValueKind == JsonValueKind.Number && flag.TryGetInt32(out var flagNumber) && flagNumber == 1)))
                    {
                        currentManager = GetString(manager, "name");
                        currentManagerClub = GetString(manager, "club_name");
                        break;
                    }
                }
                catch (JsonException) { /* Keep malformed manager rows out of the dashboard identity. */ }
            }
        }
        return new WorkspaceMetrics(
            await Count("SELECT COUNT(*) FROM LivePlayers"), await Count("SELECT COUNT(*) FROM LiveClubs"), await Count("SELECT COUNT(*) FROM LiveStaff"),
            await Count("SELECT COUNT(DISTINCT PlayerSourceId) FROM FmsavePlayerSnapshots WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)"),
            await Count("SELECT COUNT(*) FROM FmsaveClubDirectory WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)"),
            await Count("SELECT COUNT(*) FROM FmsaveStaffDirectory WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)"),
            await Count("SELECT COUNT(*) FROM PlayerAttributes"), await Count("SELECT COUNT(*) FROM LivePlayerFacts"),
            await ScalarText("SELECT CreatedUtc FROM FmsaveImportRuns ORDER BY SnapshotId DESC LIMIT 1"),
            await ScalarText("SELECT CompletedUtc FROM LiveImportRuns WHERE Status='Completed' ORDER BY RunId DESC LIMIT 1"),
            await ScalarText("SELECT SourceFileName FROM FmsaveImportRuns ORDER BY SnapshotId DESC LIMIT 1"),
            await ScalarText("SELECT Game FROM FmsaveImportRuns ORDER BY SnapshotId DESC LIMIT 1"),
            await ScalarText("SELECT GameBuild FROM FmsaveImportRuns ORDER BY SnapshotId DESC LIMIT 1"),
            knownBuild,
            await ScalarText("SELECT GameDate FROM FmsaveImportRuns ORDER BY SnapshotId DESC LIMIT 1"),
            await ScalarText("SELECT ValidationJson FROM FmsaveImportRuns ORDER BY SnapshotId DESC LIMIT 1"),
            currentManager, currentManagerClub);
    }

    public async Task<List<AtlasStaffRecord>> SearchFmsaveStaffAsync(string? search)
    {
        var q = (search ?? string.Empty).Trim();
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var rows = new List<AtlasStaffRecord>();
        var save = db.CreateCommand();
        save.CommandText = """
            SELECT s.StaffUid,s.Name,s.ClubName,s.JobTitle,s.Age,s.Nation,'Save snapshot'
            FROM FmsaveStaffDirectory s
            WHERE s.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)
              AND ($q='' OR s.Name LIKE $p OR COALESCE(s.ClubName,'') LIKE $p OR s.StaffUid LIKE $p)
            ORDER BY s.Name COLLATE NOCASE LIMIT 5000;
            """;
        save.Parameters.AddWithValue("$q", q); save.Parameters.AddWithValue("$p", $"%{q}%");
        await using (var reader = await save.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
                rows.Add(new AtlasStaffRecord(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetInt32(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6)));
        }
        var live = db.CreateCommand();
        live.CommandText = "SELECT CAST(StaffUid AS TEXT),StaffName,'FM27 live identity import' FROM LiveStaff WHERE ($q='' OR StaffName LIKE $p OR CAST(StaffUid AS TEXT) LIKE $p) ORDER BY StaffName COLLATE NOCASE LIMIT 5000;";
        live.Parameters.AddWithValue("$q", q); live.Parameters.AddWithValue("$p", $"%{q}%");
        await using (var reader = await live.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
                rows.Add(new AtlasStaffRecord(reader.GetString(0), reader.GetString(1), null, null, null, null, reader.GetString(2)));
        }
        return rows.GroupBy(x => x.SourceId, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Take(5000).ToList();
    }

    public async Task<List<AtlasFactRecord>> GetFmsaveStaffFactsAsync(string staffUid)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT RawJson FROM FmsaveStaffDirectory WHERE StaffUid=$uid AND SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) LIMIT 1;";
        cmd.Parameters.AddWithValue("$uid", staffUid);
        var json = Convert.ToString(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
        if (!string.IsNullOrWhiteSpace(json))
        {
            using var doc = JsonDocument.Parse(json);
            return FlattenScalarFacts(doc.RootElement).Where(x => !string.IsNullOrWhiteSpace(x.Key)).Take(150)
                .Select(x => new AtlasFactRecord("Staff", x.Key, x.NumericValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? x.TextValue)).ToList();
        }
        if (!long.TryParse(staffUid, out var staffId)) return new List<AtlasFactRecord>();
        var live = db.CreateCommand();
        live.CommandText = "SELECT FieldKey,NumericValue,TextValue FROM LiveStaffFacts WHERE StaffUid=$id ORDER BY FieldKey LIMIT 150;";
        live.Parameters.AddWithValue("$id", staffId);
        var facts = new List<AtlasFactRecord>();
        await using var reader = await live.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var value = reader.IsDBNull(1) ? (reader.IsDBNull(2) ? null : reader.GetString(2)) : reader.GetDouble(1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            facts.Add(new AtlasFactRecord("Live FM27 · candidate field", reader.GetString(0), value));
        }
        return facts;
    }

    public async Task<List<AtlasFactRecord>> GetLiveClubFactsAsync(string clubUid)
    {
        if (!long.TryParse(clubUid, out var clubId)) return new List<AtlasFactRecord>();
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT FieldKey,NumericValue,TextValue FROM LiveClubFacts WHERE ClubUid=$id ORDER BY FieldKey LIMIT 250;";
        cmd.Parameters.AddWithValue("$id", clubId);
        var facts = new List<AtlasFactRecord>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var value = reader.IsDBNull(1) ? (reader.IsDBNull(2) ? null : reader.GetString(2)) : reader.GetDouble(1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            facts.Add(new AtlasFactRecord("Live FM27 · candidate field", reader.GetString(0), value));
        }
        return facts;
    }

    public async Task<List<AtlasFactRecord>> GetFmsaveClubFactsAsync(string clubUid)
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        var facts = new List<AtlasFactRecord>();
        var club = db.CreateCommand();
        club.CommandText = "SELECT RawJson FROM FmsaveClubDirectory WHERE ClubUid=$uid AND SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) LIMIT 1;";
        club.Parameters.AddWithValue("$uid", clubUid);
        var json = Convert.ToString(await club.ExecuteScalarAsync());
        if (!string.IsNullOrWhiteSpace(json))
        {
            using var doc = JsonDocument.Parse(json);
            facts.AddRange(FlattenScalarFacts(doc.RootElement).Take(120).Select(x => new AtlasFactRecord("Club", x.Key, x.NumericValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? x.TextValue)));
        }

        var related = db.CreateCommand();
        related.CommandText = "SELECT TableName,RawJson FROM FmsaveExtraTables WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) AND TableName IN ('finances','facilities','stadiums','affiliates','managed_clubs');";
        await using var reader = await related.ExecuteReaderAsync();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var table = reader.GetString(0); var raw = reader.GetString(1);
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (!ReferencesClub(doc.RootElement, clubUid)) continue;
                foreach (var item in FlattenScalarFacts(doc.RootElement).Take(80))
                    facts.Add(new AtlasFactRecord(table, item.Key, item.NumericValue?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? item.TextValue));
            }
            catch (JsonException) { /* A malformed optional row does not stop the rest of the club profile. */ }
            if (facts.Count >= 400) break;
        }
        return facts;
    }

    private static bool ReferencesClub(JsonElement element, string clubUid)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals("club_uid", StringComparison.OrdinalIgnoreCase) || property.Name.Equals("owner_club_uid", StringComparison.OrdinalIgnoreCase) || property.Name.Equals("parent_club_uid", StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Value.ToString() == clubUid) return true;
                }
                if (property.Name.Equals("club_uids", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Array && property.Value.EnumerateArray().Any(x => x.ToString() == clubUid)) return true;
                if (ReferencesClub(property.Value, clubUid)) return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) if (ReferencesClub(child, clubUid)) return true;
        }
        return false;
    }

    public async Task<List<AtlasClubRecord>> SearchClubDirectoryAsync(string? search, int limit = 250, int offset = 0)
    {
        var q = (search ?? string.Empty).Trim();
        var safeLimit = Math.Clamp(limit, 1, 1000);
        var fetch = Math.Clamp(offset + safeLimit, safeLimit, 100000);
        var records = new List<AtlasClubRecord>();

        await using (var db = new SqliteConnection(_connectionString))
        {
            await db.OpenAsync().ConfigureAwait(false);
            var latest = db.CreateCommand();
            latest.CommandText = "SELECT MAX(SnapshotId) FROM FmsaveImportRuns;";
            var latestId = await latest.ExecuteScalarAsync().ConfigureAwait(false);
            if (latestId is not null && latestId != DBNull.Value)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = """
                    SELECT c.ClubUid,c.Name,c.Nation,
                      (SELECT COUNT(*) FROM FmsavePlayerSnapshots p JOIN Players x ON x.SourceId=p.PlayerSourceId WHERE p.SnapshotId=c.SnapshotId AND x.SaveClubUid=c.ClubUid)
                    FROM FmsaveClubDirectory c
                    WHERE c.SnapshotId=$id AND ($q='' OR c.Name LIKE $p OR c.ClubUid LIKE $p)
                    ORDER BY c.Name COLLATE NOCASE LIMIT $limit;
                    """;
                cmd.Parameters.AddWithValue("$id", Convert.ToInt32(latestId));
                cmd.Parameters.AddWithValue("$q", q);
                cmd.Parameters.AddWithValue("$p", $"%{q}%");
                cmd.Parameters.AddWithValue("$limit", fetch);
                await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await r.ReadAsync().ConfigureAwait(false))
                    records.Add(new AtlasClubRecord(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetInt32(3), "FMSave snapshot"));
            }
        }

        var live = await SearchLiveClubsAsync(q, fetch).ConfigureAwait(false);
        records.AddRange(live.Select(x => new AtlasClubRecord(x.ClubUid.ToString(), x.Name, null, x.LinkedPlayers, "FM27 live identity import · " + x.ValidationStatus)));

        // Keep the richest save row when the same club is represented in both sources.
        var merged = records
            .GroupBy(x => (Name: x.Name.Trim().ToUpperInvariant(), Nation: (x.Nation ?? string.Empty).Trim().ToUpperInvariant()))
            .Select(group => group.OrderByDescending(x => x.DataSource.StartsWith("FMSave", StringComparison.OrdinalIgnoreCase)).First())
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(Math.Max(0, offset)).Take(safeLimit).ToList();
        return merged;
    }

    public async Task<int> CountClubDirectoryAsync(string? search = null)
    {
        var q = (search ?? string.Empty).Trim();
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var save = db.CreateCommand();
        save.CommandText = "SELECT COUNT(*) FROM FmsaveClubDirectory WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) AND ($q='' OR Name LIKE $p OR ClubUid LIKE $p);";
        save.Parameters.AddWithValue("$q", q); save.Parameters.AddWithValue("$p", $"%{q}%");
        var saveCount = Convert.ToInt32(await save.ExecuteScalarAsync().ConfigureAwait(false));
        await db.CloseAsync().ConfigureAwait(false);
        var liveCount = await CountLiveClubsAsync(q).ConfigureAwait(false);
        // Source counts are intentionally summed: the UI labels them as records across sources,
        // since identity rows from memory and save snapshots are not guaranteed to share stable IDs.
        return saveCount + liveCount;
    }

    public async Task<List<AtlasHistoryRecord>> GetRecentEditHistoryAsync(int limit = 250)
    {
        await using var db=new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        var cmd=db.CreateCommand(); cmd.CommandText="SELECT Id,SourceId,PlayerName,FieldName,OldValue,NewValue,ChangedUtc,ChangeSource FROM EditHistory ORDER BY ChangedUtc DESC LIMIT $limit;"; cmd.Parameters.AddWithValue("$limit",Math.Clamp(limit,1,2000));
        var rows=new List<AtlasHistoryRecord>(); await using var r=await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while(await r.ReadAsync().ConfigureAwait(false)) rows.Add(new AtlasHistoryRecord(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),r.IsDBNull(5)?null:r.GetString(5),r.GetString(6),r.GetString(7)));
        return rows;
    }

    public async Task<List<(string TableName,int RowCount)>> GetFmsaveExtraTableCountsAsync()
    {
        await using var db=new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        var cmd=db.CreateCommand(); cmd.CommandText="SELECT TableName,COUNT(*) FROM FmsaveExtraTables WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) GROUP BY TableName ORDER BY TableName;";
        var rows=new List<(string,int)>(); await using var r=await cmd.ExecuteReaderAsync().ConfigureAwait(false); while(await r.ReadAsync().ConfigureAwait(false)) rows.Add((r.GetString(0),r.GetInt32(1))); return rows;
    }


    public async Task CheckpointAsync()
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(PASSIVE);";
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task<int> MassEditLocalAttributeAsync(string? search, string attributeName, string category, double value, int maxRecords = 5000, string operation = "Set")
    {
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
        if (string.IsNullOrWhiteSpace(attributeName)) throw new ArgumentException("Choose an attribute.", nameof(attributeName));
        if (double.IsNaN(value) || double.IsInfinity(value) || (operation.Equals("Set", StringComparison.OrdinalIgnoreCase) && (value < 0 || value > 20)))
            throw new ArgumentOutOfRangeException(nameof(value), "Set values must be between 0 and 20; Add and Scale accept signed adjustments.");
        if (!new[] { "Set", "Add", "Scale" }.Contains(operation, StringComparer.OrdinalIgnoreCase)) throw new ArgumentOutOfRangeException(nameof(operation));
        var q = (search ?? string.Empty).Trim();
        var limit = Math.Clamp(maxRecords, 1, 50000);
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        await using var tx = await db.BeginTransactionAsync().ConfigureAwait(false);
        var select = db.CreateCommand(); select.Transaction = (SqliteTransaction)tx;
        select.CommandText = "SELECT SourceId,Name FROM Players WHERE ($q='' OR Name LIKE $p OR Club LIKE $p OR SourceId LIKE $p) ORDER BY Name COLLATE NOCASE LIMIT $limit;";
        select.Parameters.AddWithValue("$q", q); select.Parameters.AddWithValue("$p", $"%{q}%"); select.Parameters.AddWithValue("$limit", limit);
        var targets = new List<(string Id,string Name)>();
        await using (var r = await select.ExecuteReaderAsync().ConfigureAwait(false))
            while (await r.ReadAsync().ConfigureAwait(false)) targets.Add((r.GetString(0),r.GetString(1)));
        if (targets.Count == 0) return 0;
        var read = db.CreateCommand(); read.Transaction = (SqliteTransaction)tx;
        read.CommandText = "SELECT AttributeValue FROM PlayerAttributes WHERE SourceId=$id AND AttributeName=$field;";
        read.Parameters.Add("$id", SqliteType.Text); read.Parameters.Add("$field", SqliteType.Text);
        var upsert = db.CreateCommand(); upsert.Transaction = (SqliteTransaction)tx;
        upsert.CommandText = "INSERT INTO PlayerAttributes(SourceId,AttributeName,Category,AttributeValue,UpdatedUtc) VALUES($id,$field,$category,$value,$utc) ON CONFLICT(SourceId,AttributeName) DO UPDATE SET Category=excluded.Category,AttributeValue=excluded.AttributeValue,UpdatedUtc=excluded.UpdatedUtc;";
        upsert.Parameters.Add("$id",SqliteType.Text); upsert.Parameters.Add("$field",SqliteType.Text); upsert.Parameters.Add("$category",SqliteType.Text); upsert.Parameters.Add("$value",SqliteType.Real); upsert.Parameters.Add("$utc",SqliteType.Text);
        var audit = db.CreateCommand(); audit.Transaction = (SqliteTransaction)tx;
        audit.CommandText = "INSERT INTO EditHistory(SourceId,PlayerName,FieldName,OldValue,NewValue,ChangedUtc,ChangeSource) VALUES($id,$name,$field,$old,$new,$utc,'FM Atlas 27 mass edit · local database only');";
        audit.Parameters.Add("$id",SqliteType.Text); audit.Parameters.Add("$name",SqliteType.Text); audit.Parameters.Add("$field",SqliteType.Text); audit.Parameters.Add("$old",SqliteType.Text); audit.Parameters.Add("$new",SqliteType.Text); audit.Parameters.Add("$utc",SqliteType.Text);
        var utc = DateTime.UtcNow.ToString("O");
        var changedCount = 0;
        foreach (var target in targets)
        {
            read.Parameters["$id"].Value = target.Id; read.Parameters["$field"].Value = attributeName;
            var old = await read.ExecuteScalarAsync().ConfigureAwait(false);
            double? previous = old is null || old == DBNull.Value ? null : Convert.ToDouble(old, System.Globalization.CultureInfo.InvariantCulture);
            if (!operation.Equals("Set", StringComparison.OrdinalIgnoreCase) && !previous.HasValue) continue;
            var next = operation.ToUpperInvariant() switch
            {
                "ADD" => Math.Clamp(previous!.Value + value, 0, 20),
                "SCALE" => Math.Clamp(previous!.Value * (1 + value / 100d), 0, 20),
                _ => value
            };
            if (previous.HasValue && Math.Abs(previous.Value - next) < 0.0001) continue;
            upsert.Parameters["$id"].Value=target.Id; upsert.Parameters["$field"].Value=attributeName; upsert.Parameters["$category"].Value=category; upsert.Parameters["$value"].Value=next; upsert.Parameters["$utc"].Value=utc;
            await upsert.ExecuteNonQueryAsync().ConfigureAwait(false);
            audit.Parameters["$id"].Value=target.Id; audit.Parameters["$name"].Value=target.Name; audit.Parameters["$field"].Value=attributeName;
            audit.Parameters["$old"].Value=previous.HasValue ? previous.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : DBNull.Value;
            audit.Parameters["$new"].Value=next.ToString(System.Globalization.CultureInfo.InvariantCulture); audit.Parameters["$utc"].Value=utc;
            await audit.ExecuteNonQueryAsync().ConfigureAwait(false);
            changedCount++;
        }
        await tx.CommitAsync().ConfigureAwait(false);
        return changedCount;
    
        }
        finally
        {
            WriteGate.Release();
        }
}

    public async Task<List<StatMetricField>> GetSeasonMetricCatalogAsync()
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var discovered = new Dictionary<string, (HashSet<string> Tables, int Count)>(StringComparer.OrdinalIgnoreCase);
        var indexed = db.CreateCommand();
        indexed.CommandText = "SELECT TableName,FieldKey,COUNT(*) FROM FmsaveStatFacts WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) GROUP BY TableName,FieldKey;";
        await using (var reader = await indexed.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var table = reader.GetString(0); var key = reader.GetString(1); var count = reader.GetInt32(2);
                if (!discovered.TryGetValue(key, out var entry)) entry = (new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
                entry.Tables.Add(table); discovered[key] = (entry.Tables, entry.Count + count);
            }
        }
        // Legacy fallback for snapshots imported by versions before the stat-fact index existed.
        if (discovered.Count == 0)
        {
            var legacy = db.CreateCommand();
            legacy.CommandText = "SELECT TableName,RawJson FROM FmsaveExtraTables WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) AND TableName IN ('player_season_stats','player_season_history','player_match_stats') ORDER BY RowIndex;";
            await using var reader = await legacy.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                try
                {
                    var table = reader.GetString(0); using var doc = JsonDocument.Parse(reader.GetString(1));
                    foreach (var field in EnumerateNumericPaths(doc.RootElement))
                    {
                        var leaf = field.Key.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? field.Key;
                        if (IsAnalyticsIdentityKey(leaf) || IsNonMetricNumericField(leaf)) continue;
                        if (!discovered.TryGetValue(field.Key, out var entry)) entry = (new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
                        entry.Tables.Add(table); discovered[field.Key] = (entry.Tables, entry.Count + 1);
                    }
                }
                catch { }
            }
        }
        var external = db.CreateCommand();
        external.CommandText = "SELECT RawJson FROM ExternalStatRows WHERE ImportId=(SELECT MAX(ImportId) FROM ExternalStatImportRuns) ORDER BY RowIndex;";
        await using (var externalReader = await external.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await externalReader.ReadAsync().ConfigureAwait(false))
            {
                try
                {
                    using var doc = JsonDocument.Parse(externalReader.GetString(0));
                    foreach (var field in EnumerateNumericPaths(doc.RootElement))
                    {
                        var leaf = field.Key.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? field.Key;
                        if (IsAnalyticsIdentityKey(leaf) || IsNonMetricNumericField(leaf)) continue;
                        if (!discovered.TryGetValue(field.Key, out var entry)) entry = (new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
                        entry.Tables.Add("FM View Export"); discovered[field.Key] = (entry.Tables, entry.Count + 1);
                    }
                }
                catch { }
            }
        }
        return discovered.Select(pair =>
        {
            var leaf = pair.Key.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? pair.Key;
            var category = StatCategory(leaf, pair.Key);
            var supportsPer90 = !IsAlreadyRateMetric(leaf) && !IsNonRateField(leaf);
            return new StatMetricField(pair.Key, HumanizeMetric(leaf), category, string.Join(",", pair.Value.Tables.OrderBy(x => x)), pair.Value.Count, supportsPer90);
        }).OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<List<StatSearchResult>> SearchSeasonStatCriteriaAsync(IReadOnlyList<StatFilterCriterion> criteria, bool per90, int minimumMinutes = 450, int limit = 500, bool matchAll = true)
    {
        if (criteria.Count == 0) return new();
        if (criteria.Count > 60) throw new ArgumentOutOfRangeException(nameof(criteria), "Use up to 60 metric conditions in one search.");
        if (criteria.Any(x => string.IsNullOrWhiteSpace(x.Key) || (!x.Minimum.HasValue && !x.Maximum.HasValue)))
            throw new ArgumentException("Each metric condition needs a field and at least one numeric boundary.", nameof(criteria));
        if (criteria.Any(x => x.Minimum.HasValue && x.Maximum.HasValue && x.Minimum > x.Maximum))
            throw new ArgumentException("A metric minimum cannot exceed its maximum.", nameof(criteria));

        var safeLimit = Math.Clamp(limit, 1, 5000);
        var minMinutes = Math.Clamp(minimumMinutes, 1, 10000);
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var snapshotCmd = db.CreateCommand(); snapshotCmd.CommandText = "SELECT MAX(SnapshotId) FROM FmsaveImportRuns;";
        var snapshotValue = await snapshotCmd.ExecuteScalarAsync().ConfigureAwait(false);
        var snapshotId = snapshotValue is null || snapshotValue == DBNull.Value ? (int?)null : Convert.ToInt32(snapshotValue);

        var playerMap = new Dictionary<string, (string SourceId, string Name, string? Club)>(StringComparer.OrdinalIgnoreCase);
        if (snapshotId.HasValue)
        {
            var playerCmd = db.CreateCommand();
            playerCmd.CommandText = "SELECT s.PlayerUid,COALESCE(s.PlayerSourceId,''),COALESCE(p.Name,'Unknown player'),p.Club FROM FmsavePlayerSnapshots s LEFT JOIN Players p ON p.SourceId=s.PlayerSourceId WHERE s.SnapshotId=$s;";
            playerCmd.Parameters.AddWithValue("$s", snapshotId.Value);
            await using (var r = await playerCmd.ExecuteReaderAsync().ConfigureAwait(false))
                while (await r.ReadAsync().ConfigureAwait(false)) playerMap[r.GetString(0)] = (r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3));
        }
        var extMap = db.CreateCommand();
        extMap.CommandText = "SELECT DISTINCT PlayerIdentity,PlayerSourceId,PlayerName,Club FROM ExternalStatRows WHERE ImportId=(SELECT MAX(ImportId) FROM ExternalStatImportRuns);";
        await using (var r = await extMap.ExecuteReaderAsync().ConfigureAwait(false))
            while (await r.ReadAsync().ConfigureAwait(false)) playerMap[r.GetString(0)] = (r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3));

        var rowsByIdentity = new Dictionary<string, StatWorkingRow>(StringComparer.OrdinalIgnoreCase);
        var extra = db.CreateCommand();
        extra.CommandText = snapshotId.HasValue
            ? "SELECT TableName,RowIndex,RawJson FROM FmsaveExtraTables WHERE SnapshotId=$s AND TableName IN ('player_season_stats','player_season_history','player_match_stats') ORDER BY CASE TableName WHEN 'player_season_stats' THEN 0 WHEN 'player_match_stats' THEN 1 ELSE 2 END,RowIndex;"
            : "SELECT TableName,RowIndex,RawJson FROM FmsaveExtraTables WHERE 1=0;";
        extra.Parameters.AddWithValue("$s", (object?)snapshotId ?? DBNull.Value);
        string[] minuteKeys = { "minutes_played", "playing_time_minutes", "minutes", "mins_played", "total_minutes", "minutes_on_pitch" };
        var statIndexCount = 0;
        if (snapshotId.HasValue)
        {
            var countCmd = db.CreateCommand(); countCmd.CommandText = "SELECT COUNT(*) FROM FmsaveStatFacts WHERE SnapshotId=$s;"; countCmd.Parameters.AddWithValue("$s", snapshotId.Value);
            statIndexCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync().ConfigureAwait(false));
        }
        if (statIndexCount > 0 && criteria.Count > 0)
        {
            var indexedRows = db.CreateCommand();
            var keyParameters = new List<string>();
            for (var i = 0; i < criteria.Count; i++) { var name = "$metric" + i; keyParameters.Add(name); indexedRows.Parameters.AddWithValue(name, criteria[i].Key); }
            indexedRows.CommandText = $"SELECT TableName,RowIndex,PlayerUid,PeriodKey,Minutes,FieldKey,NumericValue FROM FmsaveStatFacts WHERE SnapshotId=$s AND FieldKey COLLATE NOCASE IN ({string.Join(",", keyParameters)}) ORDER BY TableName,RowIndex;";
            indexedRows.Parameters.AddWithValue("$s", snapshotId!.Value);
            await using var indexReader = await indexedRows.ExecuteReaderAsync().ConfigureAwait(false);
            while (await indexReader.ReadAsync().ConfigureAwait(false))
            {
                var table = indexReader.GetString(0); var rowIndex = indexReader.GetInt32(1); var uid = indexReader.GetString(2);
                var period = indexReader.GetString(3); var minutes = indexReader.IsDBNull(4) ? (double?)null : indexReader.GetDouble(4);
                var key = indexReader.GetString(5); var value = indexReader.GetDouble(6);
                var identity = uid + "\0" + table + "\0" + rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!rowsByIdentity.TryGetValue(identity, out var work)) { work = new StatWorkingRow(uid, period, table) { Minutes = minutes }; rowsByIdentity[identity] = work; }
                if (work.Values.TryGetValue(key, out var existing) && Math.Abs(existing - value) > 0.0000001) continue;
                work.Values[key] = value;
            }
        }
        else
        {
        await using (var r = await extra.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await r.ReadAsync().ConfigureAwait(false))
            {
                try
                {
                    var table = r.GetString(0);
                    var rowIndex = r.GetInt32(1);
                    using var doc = JsonDocument.Parse(r.GetString(2));
                    var row = doc.RootElement;
                    var uid = GetFirstJsonString(row, "player_uid", "player_id", "uid", "unique_id", "person_uid");
                    if (string.IsNullOrWhiteSpace(uid)) continue;
                    var period = table == "player_match_stats"
                        ? (GetFirstJsonString(row, "fixture_uid", "match_uid", "fixture_id", "match_id", "date", "match_date") ?? $"Match #{rowIndex + 1}")
                        : (GetFirstJsonString(row, "season", "season_name", "year") ?? "Season unavailable");
                    var identity = uid + "\0" + (table == "player_match_stats" ? "match" : "season") + "\0" + period;
                    if (!rowsByIdentity.TryGetValue(identity, out var work))
                    {
                        work = new StatWorkingRow(uid, period, table);
                        rowsByIdentity[identity] = work;
                    }
                    if (!work.Minutes.HasValue)
                    {
                        foreach (var minuteKey in minuteKeys)
                            if (TryFindNumeric(row, minuteKey, out var mins) && mins > 0) { work.Minutes = mins; break; }
                    }
                    foreach (var criterion in criteria)
                    {
                        if (work.Values.ContainsKey(criterion.Key)) continue;
                        if (TryFindNumericPath(row, criterion.Key, out var numeric) && double.IsFinite(numeric)) work.Values[criterion.Key] = numeric;
                    }
                }
                catch { /* Skip an isolated unsupported row; preserve other imported records. */ }
            }
        }
        }
        var externalRows = db.CreateCommand();
        externalRows.CommandText = "SELECT RowIndex,RawJson FROM ExternalStatRows WHERE ImportId=(SELECT MAX(ImportId) FROM ExternalStatImportRuns) ORDER BY RowIndex;";
        await using (var r = await externalRows.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await r.ReadAsync().ConfigureAwait(false))
            {
                try
                {
                    var rowIndex = r.GetInt32(0);
                    using var doc = JsonDocument.Parse(r.GetString(1));
                    var row = doc.RootElement;
                    var uid = GetFirstJsonString(row, "player_uid", "player_id", "uid", "unique_id", "person_uid", "Unique ID");
                    if (string.IsNullOrWhiteSpace(uid)) continue;
                    var period = GetFirstJsonString(row, "season", "season_name", "year") ?? "FM View Export";
                    var identity = uid + "\0season\0" + period;
                    if (!rowsByIdentity.TryGetValue(identity, out var work))
                    {
                        work = new StatWorkingRow(uid, period, "external_view");
                        rowsByIdentity[identity] = work;
                    }
                    if (!work.Minutes.HasValue)
                        foreach (var minuteKey in minuteKeys) if (TryFindNumeric(row, minuteKey, out var mins) && mins > 0) { work.Minutes = mins; break; }
                    foreach (var criterion in criteria)
                        if (!work.Values.ContainsKey(criterion.Key) && TryFindNumericPath(row, criterion.Key, out var numeric) && double.IsFinite(numeric)) work.Values[criterion.Key] = numeric;
                }
                catch { }
            }
        }

        var results = new List<StatSearchResult>();
        foreach (var work in rowsByIdentity.Values)
        {
            if (per90 && (!work.Minutes.HasValue || work.Minutes.Value < minMinutes)) continue;
            var displayValues = new List<string>();
            var matches = matchAll;
            foreach (var criterion in criteria)
            {
                var conditionMatches = work.Values.TryGetValue(criterion.Key, out var raw);
                var value = conditionMatches ? (per90 ? raw * 90d / work.Minutes!.Value : raw) : 0d;
                conditionMatches = conditionMatches && double.IsFinite(value)
                    && (!criterion.Minimum.HasValue || value >= criterion.Minimum.Value)
                    && (!criterion.Maximum.HasValue || value <= criterion.Maximum.Value);
                if (conditionMatches)
                    displayValues.Add($"{criterion.DisplayName}: {value:0.##}");
                if (matchAll && !conditionMatches) { matches = false; break; }
                if (!matchAll && conditionMatches) matches = true;
            }
            if (!matches || displayValues.Count == 0 || !playerMap.TryGetValue(work.PlayerUid, out var player) || string.IsNullOrWhiteSpace(player.SourceId)) continue;
            results.Add(new StatSearchResult(player.SourceId, player.Name, player.Club, work.Period, work.Minutes, string.Join("  ·  ", displayValues)));
        }
        return results.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Take(safeLimit).ToList();
    }

    public async Task<List<ValueFinderCandidate>> GetValueFinderCandidatesAsync(
        int? maxAge = null, double? maxMarketValue = null, int? minCurrentAbility = null, int minimumPotentialUpside = 0, string? position = null, int limit = 1000)
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT p.SourceId,p.Name,p.Age,p.Club,p.Nation,p.League,p.Position,p.CurrentAbility,p.PotentialAbility,p.Value,
                   p.HomeGrownClub,p.HomeGrownNation,p.DataOrigin,p.PreferredFoot,p.LeftFoot,p.RightFoot
            FROM Players p
            INNER JOIN FmsavePlayerSnapshots s ON s.PlayerSourceId=p.SourceId
                AND s.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)
            WHERE p.Value IS NOT NULL AND p.Value > 0
              AND p.CurrentAbility BETWEEN 1 AND 200
              AND p.PotentialAbility BETWEEN p.CurrentAbility AND 200
            ORDER BY p.Name COLLATE NOCASE;
            """;
        var players = new List<PlayerRecord>();
        await using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
                players.Add(new PlayerRecord
                {
                    SourceId = reader.GetString(0), Name = reader.GetString(1), Age = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    Club = reader.IsDBNull(3) ? null : reader.GetString(3), Nation = reader.IsDBNull(4) ? null : reader.GetString(4),
                    League = reader.IsDBNull(5) ? null : reader.GetString(5), Position = reader.IsDBNull(6) ? null : reader.GetString(6),
                    CurrentAbility = reader.IsDBNull(7) ? null : reader.GetInt32(7), PotentialAbility = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    Value = reader.IsDBNull(9) ? null : Convert.ToDecimal(reader.GetValue(9), System.Globalization.CultureInfo.InvariantCulture),
                    HomeGrownClub = reader.IsDBNull(10) ? null : reader.GetInt32(10) == 1,
                    HomeGrownNation = reader.IsDBNull(11) ? null : reader.GetInt32(11) == 1,
                    DataOrigin = reader.GetString(12), PreferredFoot = reader.IsDBNull(13) ? null : reader.GetString(13),
                    LeftFoot = reader.IsDBNull(14) ? null : reader.GetInt32(14), RightFoot = reader.IsDBNull(15) ? null : reader.GetInt32(15)
                });
        }
        if (players.Count == 0) return new();

        // A relative, explainable screening score rather than a claim about FM's internal valuation.
        // Compare each player's CA/PA quality percentile with his market-value percentile in this
        // imported save. High score means ability/potential rank better than the price rank.
        static Dictionary<string, double> Percentiles<T>(IReadOnlyList<T> items, Func<T, double> score, Func<T, string> id)
        {
            var sorted = items.OrderBy(score).ToList();
            var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (sorted.Count == 1) { map[id(sorted[0])] = 50; return map; }
            var start = 0;
            while (start < sorted.Count)
            {
                var value = score(sorted[start]);
                var end = start + 1;
                while (end < sorted.Count && Math.Abs(score(sorted[end]) - value) < 0.0000001) end++;
                var averageRank = (start + end - 1) / 2d;
                var percentile = averageRank * 100d / (sorted.Count - 1);
                for (var i = start; i < end; i++) map[id(sorted[i])] = percentile;
                start = end;
            }
            return map;
        }
        var caPercentiles = Percentiles(players, x => x.CurrentAbility!.Value, x => x.SourceId);
        var paPercentiles = Percentiles(players, x => x.PotentialAbility!.Value, x => x.SourceId);
        var pricePercentiles = Percentiles(players, x => (double)x.Value!.Value, x => x.SourceId);
        var results = players.Select(player =>
        {
            var ability = caPercentiles[player.SourceId] * 0.65 + paPercentiles[player.SourceId] * 0.35;
            var price = pricePercentiles[player.SourceId];
            var index = Math.Clamp(50d + ability - price, 0, 100);
            return new ValueFinderCandidate(player, index, ability, price, player.PotentialAbility!.Value - player.CurrentAbility!.Value);
        });
        var positionFilter = (position ?? string.Empty).Trim();
        results = results.Where(x =>
            (!maxAge.HasValue || (x.Player.Age.HasValue && x.Player.Age.Value <= maxAge.Value)) &&
            (!maxMarketValue.HasValue || (double)x.Player.Value!.Value <= maxMarketValue.Value) &&
            (!minCurrentAbility.HasValue || x.Player.CurrentAbility >= minCurrentAbility.Value) &&
            x.PotentialUpside >= Math.Clamp(minimumPotentialUpside, 0, 100) &&
            (positionFilter.Length == 0 || (x.Player.Position?.Contains(positionFilter, StringComparison.OrdinalIgnoreCase) ?? false)));
        return results.OrderByDescending(x => x.ValueIndex).ThenByDescending(x => x.PotentialUpside).ThenBy(x => x.Player.Value).Take(Math.Clamp(limit, 1, 5000)).ToList();
    }

    public async Task<List<AdvancedSearchField>> GetAdvancedPlayerSearchFieldsAsync()
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var fields = new List<AdvancedSearchField>
        {
            new("player:Name", "Player name", "General", AdvancedSearchFieldType.Text, "Players"),
            new("player:Age", "Age", "General", AdvancedSearchFieldType.Number, "Players"),
            new("player:Club", "Club", "General", AdvancedSearchFieldType.Text, "Players"),
            new("player:Nation", "Nationality", "General", AdvancedSearchFieldType.Text, "Players"),
            new("player:League", "League", "General", AdvancedSearchFieldType.Text, "Players"),
            new("player:Position", "Position", "Position", AdvancedSearchFieldType.Text, "Players"),
            new("player:PreferredFoot", "Preferred foot", "General", AdvancedSearchFieldType.Text, "Players"),
            new("player:CurrentAbility", "Current ability", "Ability", AdvancedSearchFieldType.Number, "Players"),
            new("player:PotentialAbility", "Potential ability", "Ability", AdvancedSearchFieldType.Number, "Players"),
            new("player:Value", "Market value", "Contract & Value", AdvancedSearchFieldType.Number, "Players"),
            new("player:LeftFoot", "Left foot", "Technical", AdvancedSearchFieldType.Number, "Players"),
            new("player:RightFoot", "Right foot", "Technical", AdvancedSearchFieldType.Number, "Players"),
            new("player:HomeGrownClub", "Home-grown at club", "General", AdvancedSearchFieldType.Boolean, "Players"),
            new("player:HomeGrownNation", "Home-grown in nation", "General", AdvancedSearchFieldType.Boolean, "Players")
        };

        var attributeCommand = db.CreateCommand();
        attributeCommand.CommandText = """
            SELECT DISTINCT AttributeName,Category
            FROM PlayerAttributes
            WHERE SourceId NOT LIKE 'FMSAVE:%'
               OR SourceId IN (
                    SELECT PlayerSourceId FROM FmsavePlayerSnapshots
                    WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)
               )
            ORDER BY Category,AttributeName;
            """;
        await using (var reader = await attributeCommand.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var key = reader.GetString(0);
                var storedCategory = reader.IsDBNull(1) ? "Other" : reader.GetString(1);
                var category = storedCategory.StartsWith("attributes:", StringComparison.OrdinalIgnoreCase)
                    ? storedCategory["attributes:".Length..]
                    : storedCategory.Equals("personality", StringComparison.OrdinalIgnoreCase) ? "Personality" : storedCategory;
                fields.Add(new AdvancedSearchField("attribute:" + key, HumanizeMetric(key), category, AdvancedSearchFieldType.Number, "PlayerAttributes"));
            }
        }

        var factCommand = db.CreateCommand();
        factCommand.CommandText = """
            SELECT FactGroup,FieldKey,
                   MAX(CASE WHEN NumericValue IS NOT NULL THEN 1 ELSE 0 END),
                   MAX(CASE WHEN TextValue IS NOT NULL THEN 1 ELSE 0 END)
            FROM FmsavePlayerFacts
            WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns)
            GROUP BY FactGroup,FieldKey
            ORDER BY FactGroup,FieldKey;
            """;
        await using (var reader = await factCommand.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var group = reader.GetString(0);
                var key = reader.GetString(1);
                var hasNumeric = reader.GetInt32(2) == 1;
                var category = group switch
                {
                    "contract" => "Contract",
                    "traits" => "Traits",
                    "positions" or "natural_positions" or "accomplished_positions" => "Position",
                    "general" => "General",
                    "state" => "Status",
                    "reputation" => "Reputation",
                    "raw_attributes" => "Raw attributes",
                    _ => HumanizeMetric(group)
                };
                var fieldType = hasNumeric ? AdvancedSearchFieldType.Number : AdvancedSearchFieldType.Text;
                fields.Add(new AdvancedSearchField("fact:" + group + ":" + key, HumanizeMetric(key), category, fieldType, "FmsavePlayerFacts"));
            }
        }

        return fields
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<PlayerRecord>> SearchAdvancedPlayersAsync(
        string? search,
        IReadOnlyList<AdvancedSearchCriterion> criteria,
        bool matchAll = true,
        int limit = 250,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (criteria.Count == 0) return new();
        if (criteria.Count > 60) throw new ArgumentOutOfRangeException(nameof(criteria), "Use up to 60 conditions in one advanced player search.");

        var coreColumns = new Dictionary<string, (string Column, AdvancedSearchFieldType Type)>(StringComparer.OrdinalIgnoreCase)
        {
            ["player:Name"] = ("p.Name", AdvancedSearchFieldType.Text),
            ["player:Age"] = ("p.Age", AdvancedSearchFieldType.Number),
            ["player:Club"] = ("p.Club", AdvancedSearchFieldType.Text),
            ["player:Nation"] = ("p.Nation", AdvancedSearchFieldType.Text),
            ["player:League"] = ("p.League", AdvancedSearchFieldType.Text),
            ["player:Position"] = ("p.Position", AdvancedSearchFieldType.Text),
            ["player:PreferredFoot"] = ("p.PreferredFoot", AdvancedSearchFieldType.Text),
            ["player:CurrentAbility"] = ("p.CurrentAbility", AdvancedSearchFieldType.Number),
            ["player:PotentialAbility"] = ("p.PotentialAbility", AdvancedSearchFieldType.Number),
            ["player:Value"] = ("p.Value", AdvancedSearchFieldType.Number),
            ["player:LeftFoot"] = ("p.LeftFoot", AdvancedSearchFieldType.Number),
            ["player:RightFoot"] = ("p.RightFoot", AdvancedSearchFieldType.Number),
            ["player:HomeGrownClub"] = ("p.HomeGrownClub", AdvancedSearchFieldType.Boolean),
            ["player:HomeGrownNation"] = ("p.HomeGrownNation", AdvancedSearchFieldType.Boolean)
        };

        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        var cmd = db.CreateCommand();
        var predicates = new List<string>();

        for (var index = 0; index < criteria.Count; index++)
        {
            var criterion = criteria[index];
            var key = criterion.FieldKey?.Trim() ?? "";
            var minName = "$min" + index;
            var maxName = "$max" + index;
            var textName = "$text" + index;
            var boolName = "$bool" + index;
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A search condition has no field key.", nameof(criteria));

            if (coreColumns.TryGetValue(key, out var core))
            {
                if (core.Type == AdvancedSearchFieldType.Number)
                {
                    if (!criterion.Minimum.HasValue && !criterion.Maximum.HasValue) throw new ArgumentException($"Enter a minimum or maximum for {key}.", nameof(criteria));
                    if (criterion.Minimum.HasValue && criterion.Maximum.HasValue && criterion.Minimum > criterion.Maximum) throw new ArgumentException($"Minimum cannot exceed maximum for {key}.", nameof(criteria));
                    predicates.Add($"({core.Column} IS NOT NULL AND ({minName} IS NULL OR {core.Column}>={minName}) AND ({maxName} IS NULL OR {core.Column}<={maxName}))");
                    cmd.Parameters.AddWithValue(minName, (object?)criterion.Minimum ?? DBNull.Value);
                    cmd.Parameters.AddWithValue(maxName, (object?)criterion.Maximum ?? DBNull.Value);
                }
                else if (core.Type == AdvancedSearchFieldType.Text)
                {
                    if (string.IsNullOrWhiteSpace(criterion.TextValue)) throw new ArgumentException($"Enter text to search for {key}.", nameof(criteria));
                    predicates.Add($"(COALESCE({core.Column},'') LIKE {textName} COLLATE NOCASE)");
                    cmd.Parameters.AddWithValue(textName, "%" + criterion.TextValue.Trim() + "%");
                }
                else
                {
                    if (!criterion.BooleanValue.HasValue) throw new ArgumentException($"Choose Yes or No for {key}.", nameof(criteria));
                    predicates.Add($"({core.Column}={boolName})");
                    cmd.Parameters.AddWithValue(boolName, criterion.BooleanValue.Value ? 1 : 0);
                }
                continue;
            }

            if (key.StartsWith("attribute:", StringComparison.OrdinalIgnoreCase))
            {
                var attributeName = key["attribute:".Length..];
                if (string.IsNullOrWhiteSpace(attributeName) || (!criterion.Minimum.HasValue && !criterion.Maximum.HasValue))
                    throw new ArgumentException("Each attribute condition needs a field and a minimum or maximum.", nameof(criteria));
                if (criterion.Minimum is < 1 or > 20 || criterion.Maximum is < 1 or > 20)
                    throw new ArgumentOutOfRangeException(nameof(criteria), "Player attribute values must be between 1 and 20.");
                if (criterion.Minimum.HasValue && criterion.Maximum.HasValue && criterion.Minimum > criterion.Maximum)
                    throw new ArgumentException($"Minimum cannot exceed maximum for {attributeName}.", nameof(criteria));
                predicates.Add($"EXISTS (SELECT 1 FROM PlayerAttributes a{index} WHERE a{index}.SourceId=p.SourceId AND a{index}.AttributeName=$key{index} COLLATE NOCASE AND ($min{index} IS NULL OR a{index}.AttributeValue >= $min{index}) AND ($max{index} IS NULL OR a{index}.AttributeValue <= $max{index}))");
                cmd.Parameters.AddWithValue("$key" + index, attributeName);
                cmd.Parameters.AddWithValue(minName, (object?)criterion.Minimum ?? DBNull.Value);
                cmd.Parameters.AddWithValue(maxName, (object?)criterion.Maximum ?? DBNull.Value);
                continue;
            }

            if (key.StartsWith("fact:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = key.Split(':', 3, StringSplitOptions.None);
                if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
                    throw new ArgumentException("A save-fact search field is invalid.", nameof(criteria));
                var factGroup = parts[1];
                var factKey = parts[2];
                if (criterion.Minimum.HasValue || criterion.Maximum.HasValue)
                {
                    if (criterion.Minimum.HasValue && criterion.Maximum.HasValue && criterion.Minimum > criterion.Maximum)
                        throw new ArgumentException($"Minimum cannot exceed maximum for {factKey}.", nameof(criteria));
                    predicates.Add($"EXISTS (SELECT 1 FROM FmsavePlayerFacts f{index} WHERE f{index}.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) AND f{index}.PlayerSourceId=p.SourceId AND f{index}.FactGroup=$group{index} AND f{index}.FieldKey=$fact{index} AND f{index}.NumericValue IS NOT NULL AND ($min{index} IS NULL OR f{index}.NumericValue >= $min{index}) AND ($max{index} IS NULL OR f{index}.NumericValue <= $max{index}))");
                    cmd.Parameters.AddWithValue(minName, (object?)criterion.Minimum ?? DBNull.Value);
                    cmd.Parameters.AddWithValue(maxName, (object?)criterion.Maximum ?? DBNull.Value);
                }
                else if (!string.IsNullOrWhiteSpace(criterion.TextValue))
                {
                    predicates.Add($"EXISTS (SELECT 1 FROM FmsavePlayerFacts f{index} WHERE f{index}.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) AND f{index}.PlayerSourceId=p.SourceId AND f{index}.FactGroup=$group{index} AND f{index}.FieldKey=$fact{index} AND COALESCE(f{index}.TextValue,'') LIKE $text{index} COLLATE NOCASE)");
                    cmd.Parameters.AddWithValue(textName, "%" + criterion.TextValue.Trim() + "%");
                }
                else
                {
                    throw new ArgumentException($"Enter a value to search for {factKey}.", nameof(criteria));
                }
                cmd.Parameters.AddWithValue("$group" + index, factGroup);
                cmd.Parameters.AddWithValue("$fact" + index, factKey);
                continue;
            }

            throw new ArgumentException($"Unsupported advanced search field: {key}", nameof(criteria));
        }

        var query = (search ?? string.Empty).Trim();
        cmd.CommandText = $"""
            SELECT p.SourceId,p.Name,p.Age,p.Club,p.Nation,p.League,p.Position,p.CurrentAbility,p.PotentialAbility,p.Value,
                   p.HomeGrownClub,p.HomeGrownNation,p.DataOrigin,p.PreferredFoot,p.LeftFoot,p.RightFoot
            FROM Players p
            WHERE ($q='' OR p.Name LIKE $pattern COLLATE NOCASE OR COALESCE(p.Club,'') LIKE $pattern COLLATE NOCASE
                OR COALESCE(p.Nation,'') LIKE $pattern COLLATE NOCASE OR p.SourceId LIKE $pattern)
              AND ({string.Join(matchAll ? " AND " : " OR ", predicates)})
              AND (p.SourceId NOT LIKE 'FMSAVE:%' OR EXISTS (
                    SELECT 1 FROM FmsavePlayerSnapshots s
                    WHERE s.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) AND s.PlayerSourceId=p.SourceId
              ))
            ORDER BY CASE WHEN $q<>'' AND p.Name LIKE $prefix COLLATE NOCASE THEN 0 ELSE 1 END,p.Name COLLATE NOCASE,p.SourceId
            LIMIT $limit OFFSET $offset;
            """;
        cmd.Parameters.AddWithValue("$q", query);
        cmd.Parameters.AddWithValue("$pattern", "%" + query + "%");
        cmd.Parameters.AddWithValue("$prefix", query + "%");
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));
        cmd.Parameters.AddWithValue("$offset", Math.Clamp(offset, 0, 1_000_000));

        var results = new List<PlayerRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new PlayerRecord
            {
                SourceId = reader.GetString(0),
                Name = reader.GetString(1),
                Age = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Club = reader.IsDBNull(3) ? null : reader.GetString(3),
                Nation = reader.IsDBNull(4) ? null : reader.GetString(4),
                League = reader.IsDBNull(5) ? null : reader.GetString(5),
                Position = reader.IsDBNull(6) ? null : reader.GetString(6),
                CurrentAbility = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                PotentialAbility = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Value = reader.IsDBNull(9) ? null : Convert.ToDecimal(reader.GetValue(9), System.Globalization.CultureInfo.InvariantCulture),
                HomeGrownClub = reader.IsDBNull(10) ? null : reader.GetInt32(10) == 1,
                HomeGrownNation = reader.IsDBNull(11) ? null : reader.GetInt32(11) == 1,
                DataOrigin = reader.IsDBNull(12) ? "Unknown" : reader.GetString(12),
                PreferredFoot = reader.IsDBNull(13) ? null : reader.GetString(13),
                LeftFoot = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                RightFoot = reader.IsDBNull(15) ? null : reader.GetInt32(15)
            });
        }
        return results;
    }

    public async Task<List<PlayerRecord>> SearchPlayerAttributeCriteriaAsync(string? search, IReadOnlyList<AttributeSearchCriterion> criteria, int limit = 500, bool matchAll = true)
    {
        if (criteria.Count == 0) return new();
        if (criteria.Count > 60) throw new ArgumentOutOfRangeException(nameof(criteria), "Use up to 60 attribute conditions in one search.");
        foreach (var criterion in criteria)
        {
            if (string.IsNullOrWhiteSpace(criterion.Key) || (!criterion.Minimum.HasValue && !criterion.Maximum.HasValue)) throw new ArgumentException("Each attribute condition needs a field and at least one 1–20 boundary.");
            if (criterion.Minimum is < 1 or > 20 || criterion.Maximum is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(criteria), "Attribute values must be between 1 and 20.");
            if (criterion.Minimum.HasValue && criterion.Maximum.HasValue && criterion.Minimum > criterion.Maximum) throw new ArgumentException("An attribute minimum cannot exceed its maximum.");
        }
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        var predicates = new List<string>();
        for (var i = 0; i < criteria.Count; i++)
        {
            var c = criteria[i];
            predicates.Add($"EXISTS (SELECT 1 FROM PlayerAttributes a{i} WHERE a{i}.SourceId=p.SourceId AND lower(a{i}.AttributeName)=lower($key{i}) AND ($min{i} IS NULL OR a{i}.AttributeValue >= $min{i}) AND ($max{i} IS NULL OR a{i}.AttributeValue <= $max{i}))");
            cmd.Parameters.AddWithValue($"$key{i}", c.Key);
            cmd.Parameters.AddWithValue($"$min{i}", (object?)c.Minimum ?? DBNull.Value);
            cmd.Parameters.AddWithValue($"$max{i}", (object?)c.Maximum ?? DBNull.Value);
        }
        var q = (search ?? string.Empty).Trim();
        cmd.CommandText = $"""
            SELECT p.SourceId,p.Name,p.Age,p.Club,p.Nation,p.League,p.Position,p.CurrentAbility,p.PotentialAbility,p.Value,p.HomeGrownClub,p.HomeGrownNation,p.DataOrigin,p.PreferredFoot,p.LeftFoot,p.RightFoot
            FROM Players p
            WHERE ($q='' OR p.Name LIKE $pattern OR COALESCE(p.Club,'') LIKE $pattern OR COALESCE(p.Nation,'') LIKE $pattern OR p.SourceId LIKE $pattern)
              AND ({string.Join(matchAll ? " AND " : " OR ", predicates)})
              AND (p.SourceId NOT LIKE 'FMSAVE:%' OR EXISTS (SELECT 1 FROM FmsavePlayerSnapshots s WHERE s.SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) AND s.PlayerSourceId=p.SourceId))
            ORDER BY CASE WHEN $q<>'' AND p.Name LIKE $prefix THEN 0 ELSE 1 END,p.Name COLLATE NOCASE LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$q", q); cmd.Parameters.AddWithValue("$pattern", $"%{q}%"); cmd.Parameters.AddWithValue("$prefix", $"{q}%"); cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 5000));
        var results = new List<PlayerRecord>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            results.Add(new PlayerRecord
            {
                SourceId = reader.GetString(0), Name = reader.GetString(1), Age = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Club = reader.IsDBNull(3) ? null : reader.GetString(3), Nation = reader.IsDBNull(4) ? null : reader.GetString(4),
                League = reader.IsDBNull(5) ? null : reader.GetString(5), Position = reader.IsDBNull(6) ? null : reader.GetString(6),
                CurrentAbility = reader.IsDBNull(7) ? null : reader.GetInt32(7), PotentialAbility = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Value = reader.IsDBNull(9) ? null : Convert.ToDecimal(reader.GetValue(9), System.Globalization.CultureInfo.InvariantCulture), HomeGrownClub = !reader.IsDBNull(10) && reader.GetInt32(10) == 1,
                HomeGrownNation = !reader.IsDBNull(11) && reader.GetInt32(11) == 1, DataOrigin = reader.GetString(12),
                PreferredFoot = reader.IsDBNull(13) ? null : reader.GetString(13), LeftFoot = reader.IsDBNull(14) ? null : reader.GetInt32(14), RightFoot = reader.IsDBNull(15) ? null : reader.GetInt32(15)
            });
        }
        return results;
    }

    private sealed class StatWorkingRow(string playerUid, string period, string sourceTable)
    {
        public string PlayerUid { get; } = playerUid;
        public string Period { get; } = period;
        public string SourceTable { get; } = sourceTable;
        public double? Minutes { get; set; }
        public Dictionary<string, double> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<KeyValuePair<string, double>> EnumerateNumericPaths(JsonElement element, string prefix = "")
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var key = string.IsNullOrEmpty(prefix) ? property.Name : prefix + "." + property.Name;
                if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var number) && double.IsFinite(number)) yield return new(key, number);
                else if (property.Value.ValueKind == JsonValueKind.Object) foreach (var child in EnumerateNumericPaths(property.Value, key)) yield return child;
                else if (property.Value.ValueKind == JsonValueKind.Array)
                    foreach (var item in property.Value.EnumerateArray()) foreach (var child in EnumerateNumericPaths(item, key)) yield return child;
            }
        }
    }

    private static bool TryFindNumericPath(JsonElement element, string path, out double value)
    {
        value = 0;
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && TryFindNumericPathCore(element, parts, 0, out value);
    }

    private static bool TryFindNumericPathCore(JsonElement element, string[] parts, int index, out double value)
    {
        value = 0;
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) if (TryFindNumericPathCore(item, parts, index, out value)) return true;
            return false;
        }
        if (element.ValueKind != JsonValueKind.Object || index >= parts.Length) return false;
        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals(parts[index], StringComparison.OrdinalIgnoreCase)) continue;
            if (index == parts.Length - 1) return property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out value);
            return TryFindNumericPathCore(property.Value, parts, index + 1, out value);
        }
        return false;
    }

    private static bool IsNonMetricNumericField(string key)
    {
        var normalized = NormalizeAttributeKey(key);
        return new[] { "id", "uid", "year", "seasonid", "competitionid", "clubid", "teamid", "playerid", "squadnumber", "shirtnumber", "sortorder", "index", "ordinal" }.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAlreadyRateMetric(string key)
    {
        var n = NormalizeAttributeKey(key);
        return n.Contains("per90") || n.EndsWith("90") || n.Contains("percentage") || n.EndsWith("pct") || n.EndsWith("ratio") || n.Contains("completionrate");
    }

    private static bool IsNonRateField(string key)
    {
        var n = NormalizeAttributeKey(key);
        return n.Contains("minutes") || n.Contains("appearances") || n.Contains("starts") || n.Contains("substitute") || n.Contains("season") || n.Contains("year") || n.Contains("rating");
    }

    private static string StatCategory(string key, string path)
    {
        var n = NormalizeAttributeKey(key + " " + path);
        // Specific categories first to avoid classifying attacking penalties or defensive fouls as goalkeeper stats.
        if (new[] { "penssaved", "penaltisaved", "saves", "savepercentage", "goalsconceded", "goalsagainst", "cleansheet", "keeper", "goalkeeper" }.Any(n.Contains)) return "Goalkeeping";
        if (new[] { "yellow", "redcard", "cards", "booking", "sentoff", "foulsagainst", "foulsmade", "foul" }.Any(n.Contains)) return "Discipline";
        if (new[] { "pass", "cross", "keypass", "throughball", "progressive", "xassist", "xa", "chancecreated", "longball", "switchesofplay" }.Any(n.Contains)) return "Passing & Creation";
        if (new[] { "tackle", "interception", "clearance", "block", "duel", "aerial", "defensiveaction", "press", "recover", "ballwin", "headerswon" }.Any(n.Contains)) return "Defending";
        if (new[] { "distance", "sprint", "speed", "highintensity", "run", "acceleration", "top speed" }.Any(n.Contains)) return "Physical";
        if (new[] { "possession", "touch", "carry", "turnover", "lost", "retention", "dribble" }.Any(n.Contains)) return "Possession & Carrying";
        if (new[] { "goal", "assist", "shot", "xg", "npxg", "touchinbox", "conversion", "offside", "penaltietaken", "penaltiesscored" }.Any(n.Contains)) return "Attacking";
        if (new[] { "minutes", "appearance", "starts", "rating", "win", "draw", "loss", "points", "mvp", "pom", "games" }.Any(n.Contains)) return "General";
        return "Other metrics";
    }

    private static string HumanizeMetric(string key)
    {
        var value = key.Replace('_', ' ').Replace('-', ' ').Trim();
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(System.Text.RegularExpressions.Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " "));
    }

    public async Task<List<string>> GetSeasonMetricNamesAsync()
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT RawJson FROM FmsaveExtraTables WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) AND TableName IN ('player_season_stats','player_match_stats','player_season_history') ORDER BY TableName;";
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await r.ReadAsync().ConfigureAwait(false))
        {
            try { using var doc=JsonDocument.Parse(r.GetString(0)); CollectNumericKeys(doc.RootElement,names,0); } catch { }
            // Do not cap the registry at an arbitrary field count; retain every numeric field in the imported tables.
        }
        return names.Where(x => !IsAnalyticsIdentityKey(x)).ToList();
    }

    public async Task<List<string>> GetSeasonPer90MetricNamesAsync()
    {
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT RawJson FROM FmsaveExtraTables WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns) AND TableName IN ('player_season_stats','player_season_history') ORDER BY TableName;";
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            try { using var doc = JsonDocument.Parse(reader.GetString(0)); CollectNumericKeys(doc.RootElement, names, 0); }
            catch { /* One malformed optional row must not stop metric discovery. */ }
            // Do not cap the registry at an arbitrary field count; retain every numeric field in the imported tables.
        }
        string[] excluded = { "minutes", "minutes_played", "playing_time_minutes", "total_minutes", "mins_played", "minutes_on_pitch", "appearances", "starts", "substitute_appearances", "season", "year", "per_90", "per90", "percentage", "percent", "possession_pct", "rating" };
        return names.Where(x => !IsAnalyticsIdentityKey(x)
            && !excluded.Any(e => NormalizeAttributeKey(x).Contains(NormalizeAttributeKey(e), StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task<List<AnalyticsMetricResult>> SearchSeasonMetricAsync(string fieldKey, double? minimum, double? maximum, int limit = 250)
    {
        var safeLimit=Math.Clamp(limit,1,1000);
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync().ConfigureAwait(false);
        var snapshotCmd=db.CreateCommand(); snapshotCmd.CommandText="SELECT MAX(SnapshotId) FROM FmsaveImportRuns;";
        var snap=await snapshotCmd.ExecuteScalarAsync().ConfigureAwait(false); if(snap is null || snap==DBNull.Value) return new();
        var map=new Dictionary<string,(string Name,string? Club,string SourceId)>(StringComparer.OrdinalIgnoreCase);
        var playersCmd=db.CreateCommand(); playersCmd.CommandText="SELECT PlayerUid,PlayerSourceId FROM FmsavePlayerSnapshots WHERE SnapshotId=$s;"; playersCmd.Parameters.AddWithValue("$s",Convert.ToInt32(snap));
        await using(var pr=await playersCmd.ExecuteReaderAsync().ConfigureAwait(false)) while(await pr.ReadAsync().ConfigureAwait(false)) map[pr.GetString(0)]=(string.Empty,null,pr.IsDBNull(1)?string.Empty:pr.GetString(1));
        var extra=db.CreateCommand(); extra.CommandText="SELECT RawJson FROM FmsaveExtraTables WHERE SnapshotId=$s AND TableName IN ('player_season_stats','player_match_stats','player_season_history') ORDER BY TableName;"; extra.Parameters.AddWithValue("$s",Convert.ToInt32(snap));
        var candidates=new List<(string uid,double value,string season)>();
        await using(var er=await extra.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while(await er.ReadAsync().ConfigureAwait(false))
            {
                try
                {
                    using var doc=JsonDocument.Parse(er.GetString(0)); var root=doc.RootElement;
                    var uid=GetFirstJsonString(root,"player_uid","player_id","uid","unique_id","person_uid"); if(string.IsNullOrWhiteSpace(uid)) continue;
                    if(!TryFindNumeric(root,fieldKey,out var v)) continue;
                    if(minimum.HasValue && v<minimum.Value || maximum.HasValue && v>maximum.Value) continue;
                    var season=GetFirstJsonString(root,"season","season_name","year") ?? "Season unavailable";
                    candidates.Add((uid,v,season));
                } catch { }
            }
        }
        var results=new List<AnalyticsMetricResult>();
        foreach(var c in candidates.OrderByDescending(x=>x.value).Take(safeLimit))
        {
            if(!map.TryGetValue(c.uid,out var p)) continue;
            var nameCmd=db.CreateCommand(); nameCmd.CommandText="SELECT Name,Club FROM Players WHERE SourceId=$id LIMIT 1;"; nameCmd.Parameters.AddWithValue("$id",p.SourceId);
            await using var nr=await nameCmd.ExecuteReaderAsync().ConfigureAwait(false);
            if(await nr.ReadAsync().ConfigureAwait(false)) results.Add(new AnalyticsMetricResult(p.SourceId,nr.GetString(0),nr.IsDBNull(1)?null:nr.GetString(1),c.season,c.value));
        }
        return results;
    }

    public async Task<List<AnalyticsMetricResult>> SearchSeasonMetricPer90Async(string fieldKey, double? minimum, double? maximum, int minimumMinutes = 450, int limit = 250)
    {
        if (string.IsNullOrWhiteSpace(fieldKey)) throw new ArgumentException("Choose a statistical field.", nameof(fieldKey));
        if (minimum.HasValue && maximum.HasValue && minimum > maximum) throw new ArgumentException("The minimum cannot exceed the maximum.");
        var safeLimit = Math.Clamp(limit, 1, 1000);
        var minMinutes = Math.Clamp(minimumMinutes, 1, 10000);
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var snapshotCmd = db.CreateCommand();
        snapshotCmd.CommandText = "SELECT MAX(SnapshotId) FROM FmsaveImportRuns;";
        var snapshotValue = await snapshotCmd.ExecuteScalarAsync().ConfigureAwait(false);
        var snapshotId = snapshotValue is null || snapshotValue == DBNull.Value ? (int?)null : Convert.ToInt32(snapshotValue);
        if (!snapshotId.HasValue) return new();

        var playerMap = new Dictionary<string, (string SourceId, string Name, string? Club)>(StringComparer.OrdinalIgnoreCase);
        var players = db.CreateCommand();
        players.CommandText = "SELECT s.PlayerUid,COALESCE(s.PlayerSourceId,''),COALESCE(p.Name,'Unknown player'),p.Club FROM FmsavePlayerSnapshots s LEFT JOIN Players p ON p.SourceId=s.PlayerSourceId WHERE s.SnapshotId=$s;";
        players.Parameters.AddWithValue("$s", snapshotId.Value);
        await using (var playerReader = await players.ExecuteReaderAsync().ConfigureAwait(false))
            while (await playerReader.ReadAsync().ConfigureAwait(false))
                playerMap[playerReader.GetString(0)] = (playerReader.GetString(1), playerReader.GetString(2), playerReader.IsDBNull(3) ? null : playerReader.GetString(3));

        var matches = new List<(string Uid, string Period, string Table, double Minutes, double Value)>();
        var count = db.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM FmsaveStatFacts WHERE SnapshotId=$s;";
        count.Parameters.AddWithValue("$s", snapshotId.Value);
        var indexedCount = Convert.ToInt32(await count.ExecuteScalarAsync().ConfigureAwait(false));
        if (indexedCount > 0)
        {
            var indexed = db.CreateCommand();
            indexed.CommandText = "SELECT PlayerUid,PeriodKey,TableName,Minutes,NumericValue FROM FmsaveStatFacts WHERE SnapshotId=$s AND FieldKey=$field COLLATE NOCASE ORDER BY TableName,RowIndex;";
            indexed.Parameters.AddWithValue("$s", snapshotId.Value);
            indexed.Parameters.AddWithValue("$field", fieldKey);
            await using var reader = await indexed.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (reader.IsDBNull(3)) continue;
                var minutes = reader.GetDouble(3);
                var raw = reader.GetDouble(4);
                if (!double.IsFinite(minutes) || minutes < minMinutes || !double.IsFinite(raw)) continue;
                var per90 = raw * 90d / minutes;
                if (!double.IsFinite(per90) || (minimum.HasValue && per90 < minimum.Value) || (maximum.HasValue && per90 > maximum.Value)) continue;
                matches.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), minutes, per90));
            }
        }
        else
        {
            // Older imports pre-date the indexed facts table. Their raw JSON rows remain available.
            var legacy = db.CreateCommand();
            legacy.CommandText = "SELECT TableName,RowIndex,RawJson FROM FmsaveExtraTables WHERE SnapshotId=$s AND TableName IN ('player_season_stats','player_season_history','player_match_stats') ORDER BY TableName,RowIndex;";
            legacy.Parameters.AddWithValue("$s", snapshotId.Value);
            await using var reader = await legacy.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                try
                {
                    var table = reader.GetString(0);
                    var rowIndex = reader.GetInt32(1);
                    using var doc = JsonDocument.Parse(reader.GetString(2));
                    var row = doc.RootElement;
                    var uid = GetFirstJsonString(row, "player_uid", "player_id", "uid", "unique_id", "person_uid");
                    if (string.IsNullOrWhiteSpace(uid) || !TryFindNumericPath(row, fieldKey, out var raw) || !double.IsFinite(raw)) continue;
                    double minutes = 0;
                    foreach (var key in new[] { "minutes_played", "playing_time_minutes", "minutes", "mins_played", "total_minutes", "minutes_on_pitch" })
                        if (TryFindNumeric(row, key, out minutes) && minutes > 0) break;
                    if (minutes < minMinutes) continue;
                    var per90 = raw * 90d / minutes;
                    if (!double.IsFinite(per90) || (minimum.HasValue && per90 < minimum.Value) || (maximum.HasValue && per90 > maximum.Value)) continue;
                    var period = table == "player_match_stats"
                        ? GetFirstJsonString(row, "fixture_uid", "match_uid", "fixture_id", "match_id", "date", "match_date") ?? $"Match #{rowIndex + 1}"
                        : GetFirstJsonString(row, "season", "season_name", "year") ?? "Season unavailable";
                    matches.Add((uid, period, table, minutes, per90));
                }
                catch (JsonException) { /* Skip only the malformed optional row. */ }
            }
        }
        return matches
            .Where(x => playerMap.TryGetValue(x.Uid, out var player) && !string.IsNullOrWhiteSpace(player.SourceId))
            .GroupBy(x => x.Uid + "\0" + x.Table + "\0" + x.Period, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(x => x.Minutes).First())
            .OrderByDescending(x => x.Value)
            .Take(safeLimit)
            .Select(x => new AnalyticsMetricResult(playerMap[x.Uid].SourceId, playerMap[x.Uid].Name, playerMap[x.Uid].Club, x.Period, x.Value))
            .ToList();
    }

    public async Task<List<RoleFitCandidate>> SearchRoleFitCandidatesAsync(RoleFitProfile profile, int limit = 5000)
    {
        var weights = profile.Weights.ToDictionary(x => NormalizeAttributeKey(x.Key), x => x.Value, StringComparer.OrdinalIgnoreCase);
        if (weights.Count == 0) return new();
        var maxPlayers = Math.Clamp(limit, 100, 50000);
        await using var db = new SqliteConnection(_connectionString);
        await db.OpenAsync().ConfigureAwait(false);
        var cmd = db.CreateCommand();
        var names = weights.Keys.ToList();
        var parameterNames = new List<string>();
        for (var i = 0; i < names.Count; i++)
        {
            var parameter = "$a" + i;
            parameterNames.Add(parameter);
            cmd.Parameters.AddWithValue(parameter, names[i]);
        }
        cmd.Parameters.AddWithValue("$limit", maxPlayers);
        cmd.CommandText = $"""
            SELECT p.SourceId,p.Name,p.Club,p.Age,p.Position,p.CurrentAbility,p.PotentialAbility,a.AttributeName,a.AttributeValue
            FROM (SELECT * FROM Players WHERE SourceId NOT LIKE 'FMSAVE:%' OR SourceId IN
                (SELECT PlayerSourceId FROM FmsavePlayerSnapshots WHERE SnapshotId=(SELECT MAX(SnapshotId) FROM FmsaveImportRuns))
                ORDER BY Name COLLATE NOCASE LIMIT $limit) p
            LEFT JOIN PlayerAttributes a ON a.SourceId=p.SourceId AND LOWER(REPLACE(REPLACE(REPLACE(a.AttributeName,'_',''),' ',''),'-','')) IN ({string.Join(",", parameterNames)})
            ORDER BY p.Name COLLATE NOCASE;
            """;
        var grouped = new Dictionary<string, (string Name, string? Club, int? Age, string? Position, int? Ca, int? Pa, Dictionary<string,double> Attrs)>(StringComparer.OrdinalIgnoreCase);
        await using (var r = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
        {
            while (await r.ReadAsync().ConfigureAwait(false))
            {
                var id = r.GetString(0);
                if (!grouped.TryGetValue(id, out var row))
                    row = (r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetInt32(3), r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetInt32(5), r.IsDBNull(6) ? null : r.GetInt32(6), new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase));
                if (!r.IsDBNull(7) && !r.IsDBNull(8)) row.Attrs[NormalizeAttributeKey(r.GetString(7))] = r.GetDouble(8);
                grouped[id] = row;
            }
        }
        var results = new List<RoleFitCandidate>();
        foreach (var pair in grouped)
        {
            double weighted = 0, totalWeight = 0;
            var available = 0;
            foreach (var weight in weights)
                if (pair.Value.Attrs.TryGetValue(weight.Key, out var score) && score >= 1 && score <= 20)
                { weighted += score * weight.Value; totalWeight += weight.Value; available++; }
            if (available == 0 || totalWeight <= 0) continue;
            var coverage = available / (double)weights.Count;
            // Avoid presenting a role rating when most of its requested attributes are absent.
            if (coverage < 0.5) continue;
            results.Add(new RoleFitCandidate(pair.Key, pair.Value.Name, pair.Value.Club, pair.Value.Age, pair.Value.Position, pair.Value.Ca, pair.Value.Pa, weighted / totalWeight, available, weights.Count, coverage));
        }
        return results.OrderByDescending(x => x.Score).ThenByDescending(x => x.Coverage).Take(250).ToList();
    }

    private static string NormalizeAttributeKey(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static void CollectNumericKeys(JsonElement e, ISet<string> names, int depth)
    {
        if(depth>6) return;
        if(e.ValueKind==JsonValueKind.Object) foreach(var p in e.EnumerateObject()) { if(p.Value.ValueKind==JsonValueKind.Number && p.Value.TryGetDouble(out _)) names.Add(p.Name); else CollectNumericKeys(p.Value,names,depth+1); }
        else if(e.ValueKind==JsonValueKind.Array) foreach(var v in e.EnumerateArray().Take(20)) CollectNumericKeys(v,names,depth+1);
    }
    private static bool IsAnalyticsIdentityKey(string key) => new[]{"uid","player_uid","player_id","unique_id","id","season","year","competition_id","club_uid","team_id"}.Contains(key,StringComparer.OrdinalIgnoreCase);
    private static string? GetFirstJsonString(JsonElement e, params string[] keys)
    {
        if(e.ValueKind!=JsonValueKind.Object) return null;
        foreach(var k in keys) if(e.TryGetProperty(k,out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number) return v.ToString();
        foreach(var p in e.EnumerateObject()) if(p.Value.ValueKind==JsonValueKind.Object) { var found=GetFirstJsonString(p.Value,keys); if(found is not null) return found; }
        return null;
    }
    private static bool TryFindNumeric(JsonElement e,string key,out double value)
    {
        value=0;
        if(e.ValueKind==JsonValueKind.Object) foreach(var p in e.EnumerateObject()) { if(p.Name.Equals(key,StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind==JsonValueKind.Number && p.Value.TryGetDouble(out value)) return true; if((p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) && TryFindNumeric(p.Value,key,out value)) return true; }
        else if(e.ValueKind==JsonValueKind.Array) foreach(var v in e.EnumerateArray()) if(TryFindNumeric(v,key,out value)) return true;
        return false;
    }

    private static void SetParameter(SqliteCommand command, string parameter, object? value)
        => command.Parameters[parameter].Value = value ?? DBNull.Value;
    private static void SetNullable(SqliteParameter parameter, object? value) => parameter.Value = value ?? DBNull.Value;
    private static string? GetString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False ? value.ToString() : null;
    }
    private static long? GetLong(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name,out var value) && value.TryGetInt64(out var n) ? n : null;
    private static double? GetNumeric(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name,out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n)) return n;
        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(),System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out n)) return n;
        return null;
    }
    private static string? GetDisplayValue(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name,out var value)) return null;
        if (value.ValueKind == JsonValueKind.String) return value.GetString();
        if (value.ValueKind == JsonValueKind.Array) return string.Join(", ",value.EnumerateArray().Select(x=>x.ValueKind==JsonValueKind.String?x.GetString():x.ToString()).Where(x=>!string.IsNullOrWhiteSpace(x)));
        return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : value.ToString();
    }
    private static int GetManifestTableCount(JsonElement manifest, string tableName)
    {
        if (manifest.ValueKind != JsonValueKind.Object || !manifest.TryGetProperty("counts", out var counts) || counts.ValueKind != JsonValueKind.Object)
            return 0;
        return counts.TryGetProperty(tableName, out var value) && value.TryGetInt32(out var count) ? Math.Max(0, count) : 0;
    }

    private static async IAsyncEnumerable<JsonElement> ReadSnapshotRowsAsync(
        string inputPath,
        JsonElement legacyRoot,
        bool isBundle,
        string tableName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!isBundle)
        {
            foreach (var row in GetArray(legacyRoot, tableName))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return row;
            }
            yield break;
        }

        var tablePath = Path.Combine(inputPath, tableName + ".ndjson");
        if (!File.Exists(tablePath)) yield break;
        using var reader = new StreamReader(tablePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;
            JsonDocument rowDocument;
            try
            {
                rowDocument = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 128 });
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Invalid NDJSON row in {Path.GetFileName(tablePath)}: {ex.Message}", ex);
            }
            using (rowDocument)
            {
                yield return rowDocument.RootElement;
            }
        }
    }

    private static IEnumerable<JsonElement> GetArray(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return Array.Empty<JsonElement>();
        return EnumerateArray(value);
    }

    private static IEnumerable<JsonElement> EnumerateArray(JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
            yield return item;
    }

    private static int GetArrayCount(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;
    private static IEnumerable<(string Key, double? NumericValue, string? TextValue)> FlattenScalarFacts(JsonElement element, string prefix = "")
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
                foreach (var fact in FlattenScalarFacts(property.Value, string.IsNullOrEmpty(prefix) ? property.Name : prefix + "." + property.Name))
                    yield return fact;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var value in element.EnumerateArray())
            {
                foreach (var fact in FlattenScalarFacts(value, $"{prefix}[{index}]")) yield return fact;
                index++;
            }
        }
        else if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number))
            yield return (prefix, number, null);
        else if (element.ValueKind == JsonValueKind.String)
            yield return (prefix, null, element.GetString());
        else if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
            yield return (prefix, null, element.ToString());
    }

    private static IEnumerable<KeyValuePair<string,double>> FlattenNumeric(JsonElement element, string prefix="")
    {
        if(element.ValueKind!=JsonValueKind.Object) yield break;
        foreach(var property in element.EnumerateObject())
        {
            var key=string.IsNullOrEmpty(prefix)?property.Name:$"{prefix}.{property.Name}";
            if(property.Value.ValueKind==JsonValueKind.Number && property.Value.TryGetDouble(out var n)) yield return new KeyValuePair<string,double>(key,n);
            else if(property.Value.ValueKind==JsonValueKind.Object) foreach(var nested in FlattenNumeric(property.Value,key)) yield return nested;
        }
    }

}
