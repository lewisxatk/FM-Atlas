using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FmAtlas;

public partial class MainWindow
{
    private async Task<UIElement> DashboardPageV9Async()
    {
        var metrics = await RunDatabaseAsync(() => _database.GetWorkspaceMetricsAsync());
        var liveCareer = IsCurrentLiveCareer(metrics);
        var sourceIsFm27 = string.Equals(metrics.SaveGame, "FM27", StringComparison.OrdinalIgnoreCase);

        var page = new Grid { Margin = new Thickness(2, 2, 2, 12) };
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = page
        };
        var content = new StackPanel();
        page.Children.Add(scroll);

        TextBlock Micro(string value, bool accent = false)
            => MonolithDesignSystem.MicroLabel(value, accent);
        TextBlock Body(string value, double size = 11)
            => MonolithDesignSystem.BodyText(value, size,
                MonolithDesignSystem.Solid(MonolithDesignSystem.Muted));
        TextBlock Value(string value, double size = 25)
            => MonolithDesignSystem.DisplayText(value, size);

        Border Stat(string eyebrow, string number, string detail, string glyph)
        {
            var stack = new StackPanel();
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            stack.Children.Add(Micro(eyebrow, true));
            var icon = MonolithDesignSystem.DataText(glyph, 17,
                MonolithDesignSystem.Solid(MonolithDesignSystem.Signal));
            icon.HorizontalAlignment = HorizontalAlignment.Right;
            top.Children.Add(icon);
            stack.Children.Add(top);
            stack.Children.Add(Value(number, 27));
            stack.Children.Add(Body(detail, 10));
            return MonolithDesignSystem.Panel(stack, 10,
                new Thickness(0, 0, 9, 10), new Thickness(13), raised: true);
        }

        void Fact(Panel panel, string label, string value, string? note = null)
        {
            var grid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            var name = Body(label.ToUpperInvariant(), 9);
            name.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(name);
            var valueStack = new StackPanel();
            var displayed = MonolithDesignSystem.DataText(value, 10,
                MonolithDesignSystem.Solid(MonolithDesignSystem.Text));
            displayed.TextAlignment = TextAlignment.Right;
            displayed.TextWrapping = TextWrapping.Wrap;
            valueStack.Children.Add(displayed);
            if (!string.IsNullOrWhiteSpace(note))
            {
                var noteText = Body(note, 9);
                noteText.TextAlignment = TextAlignment.Right;
                valueStack.Children.Add(noteText);
            }
            Grid.SetColumn(valueStack, 1);
            grid.Children.Add(valueStack);
            panel.Children.Add(grid);
            panel.Children.Add(new Border
            {
                Height = 1,
                Background = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
                Opacity = 0.65
            });
        }

        // HERO — current career state and useful next actions.
        var heroGrid = new Grid { MinHeight = 154 };
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
        heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.65, GridUnitType.Star) });
        var heroCopy = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        heroCopy.Children.Add(Micro("FM ATLAS / CAREER CONTROL", true));
        heroCopy.Children.Add(Value("Your football data,\nin one workspace.", 29));
        heroCopy.Children.Add(Body(
            "Explore the loaded career, compare players and inspect the evidence behind every value. Live identities and imported save snapshots are kept distinct.",
            12));
        heroCopy.Children.Add(new Border
        {
            Height = 1,
            Background = MonolithDesignSystem.Solid(MonolithDesignSystem.Border),
            Margin = new Thickness(0, 12, 0, 9)
        });
        var stateRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        stateRow.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = MonolithDesignSystem.Solid(liveCareer
                ? MonolithDesignSystem.Positive : MonolithDesignSystem.Caution),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        stateRow.Children.Add(MonolithDesignSystem.DataText(
            liveCareer ? "SUPPORTED FM27 CAREER CONNECTED" :
            metrics.SavePlayers > 0 || metrics.SaveClubs > 0 ? "OFFLINE SAVE SNAPSHOT AVAILABLE" : "WAITING FOR CAREER DATA",
            9, MonolithDesignSystem.Solid(MonolithDesignSystem.Text)));
        stateRow.Children.Add(MonolithDesignSystem.DataText(
            liveCareer ? " / READ ONLY" : " / LOCAL",
            9, MonolithDesignSystem.Solid(MonolithDesignSystem.Muted)));
        heroCopy.Children.Add(stateRow);
        heroGrid.Children.Add(heroCopy);

        var heroActions = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(18, 0, 0, 0)
        };
        heroActions.Children.Add(Micro("QUICK ACTIONS"));
        heroActions.Children.Add(MonolithDesignSystem.ActionButton("OPEN PLAYER DATABASE",
            async (_, _) => await NavigateAsync("Players"), primary: true));
        heroActions.Children.Add(MonolithDesignSystem.ActionButton("ADVANCED SEARCH",
            async (_, _) => await NavigateAsync("Advanced Search")));
        heroActions.Children.Add(MonolithDesignSystem.ActionButton(
            liveCareer ? "REFRESH LIVE CAREER" : "IMPORT CAREER SAVE",
            async (_, _) =>
            {
                if (liveCareer) await LoadLiveCareerAsync();
                else await ImportCareerSaveAsync();
            }));
        Grid.SetColumn(heroActions, 1);
        heroGrid.Children.Add(heroActions);
        content.Children.Add(MonolithDesignSystem.Panel(heroGrid, 12,
            new Thickness(0, 0, 0, 12), new Thickness(18), raised: true));

        // SOURCE METRICS — do not combine live identities with save rows; sources
        // can overlap and don't necessarily contain equivalent profile fields.
        var stats = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 4) };
        stats.Children.Add(Stat("LIVE PLAYER IDENTITIES", metrics.LivePlayers.ToString("N0"),
            "Current process index · may overlap with save records", "L/01"));
        stats.Children.Add(Stat("SAVE PLAYER RECORDS", metrics.SavePlayers.ToString("N0"),
            "Latest imported offline snapshot", "S/02"));
        stats.Children.Add(Stat("CLUB IDENTITIES", metrics.LiveClubs.ToString("N0"),
            "Live club identity index", "C/03"));
        stats.Children.Add(Stat("IMPORTED ATTRIBUTE FACTS", metrics.LocalAttributeFacts.ToString("N0"),
            "Stored source values · missing values are not zero", "A/04"));
        content.Children.Add(stats);

        // TWO COLUMN CAREER + PROVENANCE INSPECTOR.
        var detailGrid = new Grid { Margin = new Thickness(0, 2, 0, 12) };
        detailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        detailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var careerStack = new StackPanel();
        careerStack.Children.Add(Micro("CURRENT CAREER", true));
        careerStack.Children.Add(Value(liveCareer ? "Live session" :
            metrics.SavePlayers > 0 || metrics.SaveClubs > 0 ? "Offline snapshot" : "No career loaded", 21));
        careerStack.Children.Add(Body(liveCareer
            ? "The supported FM27 process is connected. Atlas is reading the profile-bounded identity index; live editing remains locked."
            : "Import a local copy of your career save to add season, match, contract and historical information. The original save remains read-only."));
        Fact(careerStack, "Human manager",
            liveCareer ? metrics.CurrentManager ?? "Not uniquely identified" : "Not available in offline summary",
            liveCareer && !string.IsNullOrWhiteSpace(metrics.CurrentManagerClub)
                ? metrics.CurrentManagerClub : null);
        Fact(careerStack, "Game build",
            metrics.SaveBuild ?? "Unavailable",
            metrics.SaveBuildKnown == false ? "Build layout not verified" : metrics.SaveGame ?? "Game version not decoded");
        Fact(careerStack, "In-game date", metrics.SaveGameDate ?? "Unavailable");
        Fact(careerStack, "Last save import",
            metrics.LastSaveImportUtc is null ? "No import recorded" : FormatDate(metrics.LastSaveImportUtc));
        Fact(careerStack, "Last live index",
            metrics.LastLiveImportUtc is null ? "No completed scan recorded" : FormatDate(metrics.LastLiveImportUtc));
        var careerCard = MonolithDesignSystem.Panel(careerStack, 12,
            new Thickness(0, 0, 8, 0), new Thickness(15));

        var coverageStack = new StackPanel();
        coverageStack.Children.Add(Micro("SOURCE COVERAGE", true));
        coverageStack.Children.Add(Value("Know what you’re seeing.", 21));
        coverageStack.Children.Add(Body("Each count belongs to a source. A player identity is not the same as a fully decoded profile."));
        Fact(coverageStack, "Live staff identities", metrics.LiveStaff.ToString("N0"),
            "Current process index");
        Fact(coverageStack, "Save staff records", metrics.SaveStaff.ToString("N0"),
            "Imported career snapshot");
        Fact(coverageStack, "Save club records", metrics.SaveClubs.ToString("N0"),
            "Offline save snapshot; may overlap with live club identities");
        Fact(coverageStack, "Local attribute facts", metrics.LocalAttributeFacts.ToString("N0"),
            "Values retained from imported sources");
        Fact(coverageStack, "Save file",
            metrics.LastSaveFileName ?? "No file recorded");
        var coverageNotice = MonolithDesignSystem.BodyText(
            sourceIsFm27
                ? "FM27 field meanings remain build-specific. Candidate live values stay provisional until compared with the in-game UI; no unsupported statistics are generated."
                : "Unavailable fields remain unavailable. Atlas does not convert missing values to zero or invent game statistics.",
            10, MonolithDesignSystem.Solid(MonolithDesignSystem.Caution));
        coverageNotice.Margin = new Thickness(0, 12, 0, 0);
        coverageStack.Children.Add(coverageNotice);
        var coverageCard = MonolithDesignSystem.Panel(coverageStack, 12,
            new Thickness(8, 0, 0, 0), new Thickness(15));

        Grid.SetColumn(coverageCard, 1);
        detailGrid.Children.Add(careerCard);
        detailGrid.Children.Add(coverageCard);
        content.Children.Add(detailGrid);

        // WORKSPACE DIRECTORY — make the five primary destinations feel actionable.
        var directory = MonolithDesignSystem.Panel(new StackPanel(), 12,
            new Thickness(0, 0, 0, 12), new Thickness(15), raised: true);
        var directoryStack = (StackPanel)directory.Child;
        directoryStack.Children.Add(Micro("WORKSPACE DIRECTORY", true));
        directoryStack.Children.Add(Value("Choose where to work next.", 20));
        directoryStack.Children.Add(Body("The five primary areas keep navigation calm; the contextual strip exposes the detailed tools within each area."));
        var shortcuts = new UniformGrid { Columns = 4, Margin = new Thickness(0, 8, 0, 0) };
        shortcuts.Children.Add(MonolithDesignSystem.ActionButton("PLAYERS", async (_, _) => await NavigateAsync("Players")));
        shortcuts.Children.Add(MonolithDesignSystem.ActionButton("SQUAD & TACTICS", async (_, _) => await NavigateAsync("Squad Architect")));
        shortcuts.Children.Add(MonolithDesignSystem.ActionButton("CLUBS", async (_, _) => await NavigateAsync("Clubs")));
        shortcuts.Children.Add(MonolithDesignSystem.ActionButton("STAFF", async (_, _) => await NavigateAsync("Staff")));
        directoryStack.Children.Add(shortcuts);
        content.Children.Add(directory);

        return scroll;
    }
}
