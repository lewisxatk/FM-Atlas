using System.Windows;
using System.Windows.Controls;

namespace FmAtlas;

public partial class MainWindow
{
    private async Task<UIElement> AdvancedSearchWorkspaceAsync()
    {
        var attributeFields = await RunDatabaseAsync(() => _database.GetAttributeNamesAsync());
        var performanceFields = await RunDatabaseAsync(() => _database.GetSeasonMetricCatalogAsync());

        var content = new StackPanel { Margin = new Thickness(2, 0, 2, 18) };
        var introduction = Card(new StackPanel(), 18, new Thickness(0, 0, 0, 12));
        var intro = (StackPanel)introduction.Child;
        intro.Children.Add(Txt("ATLAS SEARCH STUDIO", 9, true, Teal));
        intro.Children.Add(Txt("Advanced Search", 27, true, Ink));
        intro.Children.Add(Txt("Choose the right search engine for the data you want to investigate. Each builder works from fields actually available in the selected Atlas dataset; unavailable fields are not treated as zero or guessed.", 11, false, Muted, wrap: true));
        var summary = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        summary.Children.Add(StatusPill($"{attributeFields.Count:N0} attribute / personality fields", "Imported player attributes"));
        summary.Children.Add(StatusPill($"{performanceFields.Count:N0} performance metrics", "Indexed career statistics"));
        summary.Children.Add(StatusPill("Up to 60", "Conditions per specialist builder"));
        intro.Children.Add(summary);
        content.Children.Add(introduction);

        var section = Txt("CHOOSE A SEARCH WORKSPACE", 9, true, Teal);
        section.Margin = new Thickness(5, 3, 0, 8);
        content.Children.Add(section);

        var cards = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
        cards.Children.Add(SearchEntryCard(
            "01 · PLAYER DISCOVERY",
            "Player Search",
            "Search the imported player pool by name or ID, age, position, league, nation, home-grown status, preferred foot, CA / PA and available core fields. Results are paged and open into the full player profile.",
            "Open player directory",
            async () => await NavigateAsync("Players")));
        cards.Children.Add(SearchEntryCard(
            "02 · ATTRIBUTES & PERSONALITY",
            "Attribute Search",
            "Combine up to 60 minimum / maximum conditions across the 52 standard attribute fields and available personality fields. Missing values stay missing; only fields actually supplied by the source can match.",
            "Open attribute builder",
            async () => await NavigateAsync("Attribute Search")));
        cards.Children.Add(SearchEntryCard(
            "03 · PERFORMANCE & MONEYBALL",
            "Statistical Search",
            "Build up to 60 conditions from the metrics discovered in imported season, career-history and match-stat rows. Optional per-90 normalisation uses a minimum-minutes floor.",
            "Open statistics builder",
            async () => await NavigateAsync("Stats Search")));
        cards.Children.Add(SearchEntryCard(
            "04 · MULTI-ENTITY LOOKUP",
            "Global Search",
            "Find players, staff and clubs by name or identifier, then open the corresponding workspace. Use Ctrl+K from anywhere in Atlas.",
            "Open global search",
            () => ShowGlobalSearch()));

        content.Children.Add(cards);

        var limits = Card(new StackPanel(), 16, new Thickness(0, 12, 0, 0));
        var limitStack = (StackPanel)limits.Child;
        limitStack.Children.Add(Txt("SEARCH ENGINE TRANSPARENCY", 9, true, Teal));
        limitStack.Children.Add(Txt("What this version supports", 16, true, Ink));
        limitStack.Children.Add(Txt("The attribute and performance builders support AND conditions within their own data domains. Global Search is a separate entity lookup, and Player Search uses its own indexed player fields. These engines are not yet combined into one universal query.", 10, false, Muted, wrap: true));
        limitStack.Children.Add(Txt("Not yet implemented in V8: grouped AND / OR logic across mixed field categories, saved universal filter profiles, arbitrary staff / club criteria in the same query, and user-configurable search-result columns. These remain explicit development items rather than controls that appear to work but do not.", 10, false, Muted, wrap: true));
        content.Children.Add(limits);

        var scroll = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        return scroll;
    }

    private static Border SearchEntryCard(string eyebrow, string title, string description, string action, Func<Task> click)
    {
        var body = new StackPanel();
        body.Children.Add(Txt(eyebrow, 8, true, Teal));
        body.Children.Add(Txt(title, 18, true, Ink));
        body.Children.Add(Txt(description, 10, false, Muted, wrap: true));
        var button = SoftButton(action, async (_, _) => await click());
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Margin = new Thickness(0, 13, 0, 0);
        body.Children.Add(button);
        var card = Card(body, 16, new Thickness(6));
        card.Width = 360;
        card.MinHeight = 220;
        return card;
    }

    private static Border StatusPill(string value, string tooltip)
    {
        var pill = new Border
        {
            Background = Brush("#1C242E"),
            BorderBrush = Brush("#303943"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 7, 0),
            Child = Txt(value, 9, true, Ink),
            ToolTip = tooltip
        };
        ToolTipService.SetInitialShowDelay(pill, 350);
        return pill;
    }
}
