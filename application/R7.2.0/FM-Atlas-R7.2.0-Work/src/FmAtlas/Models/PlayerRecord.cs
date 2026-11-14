namespace FmAtlas.Models;

public sealed class PlayerRecord
{
    public string SourceId { get; set; } = "";
    public string Name { get; set; } = "";
    public int? Age { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string RegenStatus { get; set; } = "Unknown";
    public string RegenConfidence { get; set; } = "Unverified";
    public string? Club { get; set; }
    public string? Nation { get; set; }
    public string? League { get; set; }
    public string? Position { get; set; }
    public string? PreferredFoot { get; set; }
    public int? LeftFoot { get; set; }
    public int? RightFoot { get; set; }
    public int? CurrentAbility { get; set; }
    public int? PotentialAbility { get; set; }
    public decimal? Value { get; set; }
    public bool? HomeGrownClub { get; set; }
    public bool? HomeGrownNation { get; set; }
    public string DataOrigin { get; set; } = "Unknown";
    public string DisplayAbility => CurrentAbility?.ToString() ?? "—";
    public string DisplayPotential => PotentialAbility?.ToString() ?? "—";
    public string DisplayValue => Value.HasValue ? Value.Value.ToString("N0") : "—";
}

public sealed class ShortlistRecord
{
    public const string DefaultStage = "Identified";
    public const string DefaultPriority = "Normal";
    public static readonly IReadOnlyList<string> PipelineStages = new[]
    {
        "Identified", "Monitoring", "Scouting", "Contact", "Offer", "Signed", "Rejected"
    };
    public static readonly IReadOnlyList<string> PipelinePriorities = new[] { "Low", "Normal", "High" };

    public string SourceId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string ShortlistName { get; set; } = "My Shortlist";
    public string? Notes { get; set; }
    public string Stage { get; set; } = DefaultStage;
    public string Priority { get; set; } = DefaultPriority;
    public double? TargetFee { get; set; }
    public double? TargetWage { get; set; }
    public string? NextAction { get; set; }
    public string? ReviewDate { get; set; }
    public DateTime AddedUtc { get; set; }
    public string DisplayClub { get; set; } = "—";
    public string DisplayPosition { get; set; } = "—";
}
