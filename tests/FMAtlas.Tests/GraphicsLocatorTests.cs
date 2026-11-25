using FMAtlas.Domain.Graphics;
using FMAtlas.Infrastructure.Graphics;
using Xunit;

namespace FMAtlas.Tests;

public sealed class GraphicsLocatorTests
{
    [Fact]
    public async Task IndexesPortraitClubAndNationGraphicsAndSupportsNewgenUids()
    {
        using var temp = new TemporaryDirectory();
        var faces = temp.CreateDirectory("graphics/faces");
        var clubs = temp.CreateDirectory("graphics/logos/clubs");
        var nations = temp.CreateDirectory("graphics/logos/nations");

        temp.Write(Path.Combine(faces, "config.xml"), Config(
            Record("face_123", "graphics/pictures/person/123/portrait")));
        temp.Write(Path.Combine(faces, "face_123.png"), "image");
        temp.Write(Path.Combine(clubs, "config.xml"), Config(
            Record("club_77", "graphics/pictures/club/77/logo")));
        temp.Write(Path.Combine(clubs, "club_77.jpg"), "image");
        temp.Write(Path.Combine(nations, "config.xml"), Config(
            Record("nation_5", "graphics/pictures/nation/5/logo")));
        temp.Write(Path.Combine(nations, "nation_5.webp"), "image");

        var locator = new FootballManagerGraphicsLocator();
        var summary = await locator.IndexAsync(Path.Combine(temp.Root, "graphics"));

        Assert.Equal(GraphicsIndexState.Ready, summary.State);
        Assert.Equal(3, summary.ConfigFiles);
        Assert.Equal(1, summary.Portraits);
        Assert.Equal(1, summary.ClubLogos);
        Assert.Equal(1, summary.NationLogos);
        Assert.EndsWith("face_123.png", locator.FindPortrait("123")!.ImagePath);
        Assert.EndsWith("face_123.png", locator.FindPortrait("r-123")!.ImagePath);
        Assert.EndsWith("club_77.jpg", locator.FindClubLogo(77)!.ImagePath);
        Assert.EndsWith("nation_5.webp", locator.FindNationLogo(5)!.ImagePath);
    }

    [Fact]
    public async Task IgnoresIconsAndUnrelatedGraphicsTargets()
    {
        using var temp = new TemporaryDirectory();
        var folder = temp.CreateDirectory("graphics/faces");

        temp.Write(Path.Combine(folder, "config.xml"), Config(
            Record("icon_12", "graphics/pictures/person/12/icon") +
            Record("kit_12", "graphics/pictures/club/12/kits/home")));

        var locator = new FootballManagerGraphicsLocator();
        var summary = await locator.IndexAsync(Path.Combine(temp.Root, "graphics"));

        Assert.Equal(GraphicsIndexState.Ready, summary.State);
        Assert.Equal(0, summary.Portraits);
        Assert.Null(locator.FindPortrait("12"));
    }

    [Fact]
    public async Task DuplicateMappingsUseFirstSortedValidConfigPath()
    {
        using var temp = new TemporaryDirectory();
        var first = temp.CreateDirectory("graphics/a-pack");
        var second = temp.CreateDirectory("graphics/z-pack");

        temp.Write(Path.Combine(first, "config.xml"), Config(
            Record("first_face", "graphics/pictures/person/42/portrait")));
        temp.Write(Path.Combine(first, "first_face.png"), "image");
        temp.Write(Path.Combine(second, "config.xml"), Config(
            Record("second_face", "graphics/pictures/person/42/portrait")));
        temp.Write(Path.Combine(second, "second_face.png"), "image");

        var locator = new FootballManagerGraphicsLocator();
        await locator.IndexAsync(Path.Combine(temp.Root, "graphics"));

        Assert.EndsWith("first_face.png", locator.FindPortrait("42")!.ImagePath);
    }

    [Fact]
    public async Task RejectsMappingsThatEscapeTheGraphicsRoot()
    {
        using var temp = new TemporaryDirectory();
        var root = temp.CreateDirectory("graphics/faces");
        var outside = temp.CreateDirectory("outside");
        temp.Write(Path.Combine(root, "config.xml"), Config(
            Record("../../outside/external_face", "graphics/pictures/person/99/portrait")));
        temp.Write(Path.Combine(outside, "external_face.png"), "image");

        var locator = new FootballManagerGraphicsLocator();
        await locator.IndexAsync(Path.Combine(temp.Root, "graphics"));

        Assert.Null(locator.FindPortrait("99"));
    }

    [Fact]
    public async Task MissingFolderProducesMissingStateWithoutResults()
    {
        using var temp = new TemporaryDirectory();
        var locator = new FootballManagerGraphicsLocator();

        var summary = await locator.IndexAsync(Path.Combine(temp.Root, "missing"));

        Assert.Equal(GraphicsIndexState.Missing, summary.State);
        Assert.Null(locator.FindPortrait("1"));
        Assert.NotEmpty(summary.Warnings);
    }

    [Fact]
    public async Task ClearTurnsTheIndexOff()
    {
        using var temp = new TemporaryDirectory();
        var folder = temp.CreateDirectory("graphics/faces");
        temp.Write(Path.Combine(folder, "config.xml"), Config(
            Record("face_1", "graphics/pictures/person/1/portrait")));
        temp.Write(Path.Combine(folder, "face_1.png"), "image");

        var locator = new FootballManagerGraphicsLocator();
        await locator.IndexAsync(Path.Combine(temp.Root, "graphics"));
        locator.Clear();

        Assert.Equal(GraphicsIndexState.Off, locator.Status.State);
        Assert.Null(locator.FindPortrait("1"));
    }

    private static string Record(string from, string to) =>
        $"<record from=\"{from}\" to=\"{to}\" />";

    private static string Config(string records) =>
        $"<record><list id=\"maps\">{records}</list></record>";

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "FMAtlas.GraphicsTests", Guid.NewGuid().ToString("N"));

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Write(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
