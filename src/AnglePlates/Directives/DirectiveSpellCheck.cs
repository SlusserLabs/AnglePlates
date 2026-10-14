using System;

namespace SlusserLabs.AnglePlates;

// The SLAP011 matcher: spots a line that looks like a misspelled directive
internal static class DirectiveSpellCheck
{
    // Returns the directive the line probably meant, such as "#model" for "#modle string", or null when it doesn't look
    // like one. The line has to start with '#' and letters followed by whitespace or the end of the line, so CSS such as
    // "#header {", Markdown headings, shebangs, and C# like "#if" pass through. A name that isn't a directive warns when it
    // equals one ignoring case, or when it's one edit away. Names longer than four letters allow two edits. An edit is an
    // insertion, a deletion, a substitution, or swapping two neighbors, so "#tpye" is one edit from "#type", and "#top"
    // stays quiet, because it's two edits from a four-letter name. The misspelled name, such as "#modle", comes back too, so
    // callers don't need their own rule for where a name ends.
    public static string? FindIntendedDirective(string line, out string misspelled)
    {
        misspelled = string.Empty;
        int end = 1;
        while (end < line.Length && line[end] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'))
        {
            end++;
        }

        if (line.Length == 0 || line[0] != TemplateSyntax.DirectivePrefix || end == 1
            || (end < line.Length && TemplateSyntax.Whitespace.IndexOf(line[end]) < 0))
        {
            return null;
        }

        var name = line.Substring(1, end - 1);
        string? best = null;
        int bestDistance = int.MaxValue;
        foreach (var kind in DirectiveKindExtensions.All)
        {
            var directive = kind.GetName();
            if (string.Equals(name, directive, StringComparison.Ordinal))
            {
                return null;
            }

            int distance = string.Equals(name, directive, StringComparison.OrdinalIgnoreCase) ? 0 : EditDistance(name, directive);
            if (distance <= (directive.Length > 4 ? 2 : 1) && distance < bestDistance)
            {
                best = directive;
                bestDistance = distance;
            }
        }

        if (best is null)
        {
            return null;
        }

        misspelled = line.Substring(0, end);

        return TemplateSyntax.DirectivePrefix + best;
    }

    // Optimal string alignment distance: Levenshtein plus swapping two neighboring letters, with no letter edited twice
    private static int EditDistance(string source, string target)
    {
        var distances = new int[source.Length + 1, target.Length + 1];
        for (int i = 0; i <= source.Length; i++)
        {
            distances[i, 0] = i;
        }

        for (int j = 0; j <= target.Length; j++)
        {
            distances[0, j] = j;
        }

        for (int i = 1; i <= source.Length; i++)
        {
            for (int j = 1; j <= target.Length; j++)
            {
                int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                int distance = Math.Min(Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1), distances[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && source[i - 1] == target[j - 2] && source[i - 2] == target[j - 1])
                {
                    distance = Math.Min(distance, distances[i - 2, j - 2] + 1);
                }

                distances[i, j] = distance;
            }
        }

        return distances[source.Length, target.Length];
    }
}
