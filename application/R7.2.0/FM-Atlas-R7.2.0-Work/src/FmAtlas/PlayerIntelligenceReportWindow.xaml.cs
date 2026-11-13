using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace FmAtlas;

public sealed class IntelligenceEvidenceRow
{
    public string Player { get; init; } = "";
    public string UID { get; init; } = "";
    public string Section { get; init; } = "";
    public string Field { get; init; } = "";
    public string Offset { get; init; } = "";
    public string Raw { get; init; } = "";
    public string Decoded { get; init; } = "";
    public string Applicability { get; init; } = "";
    public string Confidence { get; init; } = "";
    public string Notes { get; init; } = "";
}

public partial class PlayerIntelligenceReportWindow : Window
{
    private readonly ObservableCollection<IntelligenceEvidenceRow> _rows = new();
    private readonly CollectionViewSource _view = new();
    private bool _hiddenOnly;

    public PlayerIntelligenceReportWindow(string csvPath)
    {
        InitializeComponent();
        var lines = File.ReadAllLines(csvPath);
        var headerIndex = Array.FindIndex(lines, line => line.StartsWith("Player,UID,Section,Field,Offset,Raw,Decoded,Applicability,Confidence,Notes", StringComparison.Ordinal));
        if (headerIndex < 0) throw new InvalidDataException("This file is not a recognised FM Atlas full player intelligence report.");
        foreach (var line in lines.Skip(headerIndex + 1))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var cells = ParseCsvLine(line);
            if (cells.Count < 10) continue;
            _rows.Add(new IntelligenceEvidenceRow { Player=cells[0], UID=cells[1], Section=cells[2], Field=cells[3], Offset=cells[4], Raw=cells[5], Decoded=cells[6], Applicability=cells[7], Confidence=cells[8], Notes=cells[9] });
        }
        _view.Source = _rows;
        _view.Filter += (sender, args) => args.Accepted = FilterPredicate(args.Item);
        EvidenceGrid.ItemsSource = _view.View;
        var players = _rows.Select(r => r.Player).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();
        PlayerFilter.Items.Add("All players"); foreach (var name in players) PlayerFilter.Items.Add(name);
        PlayerFilter.SelectedIndex = 0;
        var sections = _rows.Select(r => r.Section).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();
        SectionFilter.Items.Add("All sections"); foreach (var section in sections) SectionFilter.Items.Add(section);
        SectionFilter.SelectedIndex = 0;
        ReportTitle.Text = "Player intelligence · " + Path.GetFileName(csvPath);
        var matched = players.Count;
        var hidden = _rows.Count(r => r.Section.Equals("Hidden", StringComparison.OrdinalIgnoreCase));
        var medical = _rows.Count(r => r.Section.StartsWith("Medical", StringComparison.OrdinalIgnoreCase));
        ReportSummary.Text = $"{matched} players · {_rows.Count:N0} evidence rows · {hidden} hidden-stat rows · {medical} medical evidence rows · read-only";
        UpdateCount();
    }

    private bool FilterPredicate(object item)
    {
        if (item is not IntelligenceEvidenceRow row) return false;
        if (_hiddenOnly && !row.Section.Equals("Hidden", StringComparison.OrdinalIgnoreCase)) return false;
        var player = PlayerFilter.SelectedItem as string;
        if (!string.IsNullOrEmpty(player) && player != "All players" && row.Player != player) return false;
        var section = SectionFilter.SelectedItem as string;
        if (!string.IsNullOrEmpty(section) && section != "All sections" && row.Section != section) return false;
        var q = SearchBox.Text?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            var haystack = string.Join(" ", row.Player,row.UID,row.Section,row.Field,row.Offset,row.Raw,row.Decoded,row.Applicability,row.Confidence,row.Notes);
            if (!haystack.Contains(q, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private void FilterChanged(object sender, RoutedEventArgs e) { _view.View.Refresh(); UpdateCount(); }
    private void ShowHidden_Click(object sender, RoutedEventArgs e) { _hiddenOnly = true; SectionFilter.SelectedItem = "All sections"; _view.View.Refresh(); UpdateCount(); }
    private void ShowAll_Click(object sender, RoutedEventArgs e) { _hiddenOnly = false; SearchBox.Clear(); PlayerFilter.SelectedIndex = 0; SectionFilter.SelectedIndex = 0; _view.View.Refresh(); UpdateCount(); }
    private void UpdateCount() { if (VisibleCount is not null) VisibleCount.Text = $"{_view.View.Cast<object>().Count():N0} records"; }

    private void EvidenceGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        e.Column.Header = e.PropertyName switch { "UID" => "PLAYER UID", "Section" => "CATEGORY", "Field" => "FIELD", "Offset" => "OFFSET", "Raw" => "RAW", "Decoded" => "DECODED", "Applicability" => "PROFILE", "Confidence" => "CONFIDENCE", _ => e.PropertyName.ToUpperInvariant() };
        e.Column.Width = e.PropertyName switch { "Player" => new DataGridLength(1.3, DataGridLengthUnitType.Star), "Field" => new DataGridLength(1.3, DataGridLengthUnitType.Star), "Notes" => new DataGridLength(3, DataGridLengthUnitType.Star), _ => new DataGridLength(1, DataGridLengthUnitType.Star) };
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>(); var current = new System.Text.StringBuilder(); bool quoted = false;
        for (int i=0;i<line.Length;i++)
        {
            char c=line[i];
            if (c=='"') { if (quoted && i+1<line.Length && line[i+1]=='"') { current.Append('"'); i++; } else quoted=!quoted; }
            else if (c==',' && !quoted) { values.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        values.Add(current.ToString()); return values;
    }
}
