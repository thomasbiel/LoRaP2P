namespace LoRaP2P.Console.Tests;

[TestFixture]
internal sealed class RadioBonnetTextLayoutTests
{
    private static readonly string[] ExpectedWrappedLines =
    [
        "First line",
        "Second line contains",
        "more than twenty-one",
        "characters"
    ];

    [Test]
    public void Wrap_PreservesExplicitLinesAndWrapsAtWordBoundary()
    {
        var lines = RadioBonnetTextLayout.Wrap(
            "First line\nSecond line contains more than twenty-one characters");

        Assert.That(
            lines,
            Is.EqualTo(ExpectedWrappedLines));
    }

    [Test]
    public void Wrap_SplitsWordsThatExceedDisplayWidth()
    {
        var lines = RadioBonnetTextLayout.Wrap("1234567890123456789012345");

        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Count.EqualTo(2));
            Assert.That(lines[0], Is.EqualTo("123456789012345678901"));
            Assert.That(lines[1], Is.EqualTo("2345"));
        });
    }
}
