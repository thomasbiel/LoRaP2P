using System.Text;

namespace LoRaP2P.Console.Commands;

internal static class CommandLineTokenizer
{
    public static string[] Tokenize(string commandLine)
    {
        List<string> arguments = [];
        StringBuilder current = new();
        var insideQuotes = false;
        var tokenStarted = false;

        for (var index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];
            if (character == '"')
            {
                insideQuotes = !insideQuotes;
                tokenStarted = true;
                continue;
            }

            if (character == '\\'
                && index + 1 < commandLine.Length
                && commandLine[index + 1] == '"')
            {
                current.Append('"');
                tokenStarted = true;
                index++;
                continue;
            }

            if (char.IsWhiteSpace(character) && !insideQuotes)
            {
                AddArgument(arguments, current, ref tokenStarted);
                continue;
            }

            current.Append(character);
            tokenStarted = true;
        }

        if (insideQuotes)
        {
            throw new FormatException("Unterminated quoted value.");
        }

        AddArgument(arguments, current, ref tokenStarted);
        return [.. arguments];
    }

    private static void AddArgument(List<string> arguments, StringBuilder current, ref bool tokenStarted)
    {
        if (!tokenStarted)
        {
            return;
        }

        arguments.Add(current.ToString());
        current.Clear();
        tokenStarted = false;
    }
}
