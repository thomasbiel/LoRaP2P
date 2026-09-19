namespace LoRaP2P.Console;

internal static class RadioBonnetTextLayout
{
    public const int CharactersPerLine = 21;
    public const int VisibleLines = 4;

    public static IReadOnlyList<string> Wrap(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<string> lines = [];

        foreach (var paragraph in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var remaining = paragraph;
            while (remaining.Length > CharactersPerLine)
            {
                var splitAt = remaining.LastIndexOf(' ', CharactersPerLine, CharactersPerLine);
                if (splitAt <= 0)
                {
                    splitAt = CharactersPerLine;
                }

                lines.Add(remaining[..splitAt].TrimEnd());
                remaining = remaining[splitAt..].TrimStart();
            }

            lines.Add(remaining);
        }

        return lines;
    }
}
