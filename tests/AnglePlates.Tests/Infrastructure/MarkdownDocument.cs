using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace SlusserLabs.AnglePlates.Tests.Infrastructure;

// Reads the links and heading anchors of a Markdown file the way GitHub renders them. Fenced code blocks and inline code
// are skipped, because a link or heading inside them is only example text.
internal sealed class MarkdownDocument
{
    // An inline link or image, [text](target "title"), with one level of brackets allowed in the text
    private static readonly Regex _inlineLink = new(@"!?\[(?:[^\[\]]|\[[^\]]*\])*\]\(\s*<?(?<target>[^)\s>]*)>?(?:\s+""[^""]*"")?\s*\)", RegexOptions.CultureInvariant);

    // A reference definition, [label]: target
    private static readonly Regex _referenceDefinition = new(@"^ {0,3}\[[^\]]+\]:\s*<?(?<target>[^\s>]+)>?", RegexOptions.CultureInvariant);

    // A run of backticks and everything up to the same run, which is one inline code span
    private static readonly Regex _inlineCode = new(@"(`+).*?\1", RegexOptions.CultureInvariant);

    // A link inside a heading, which GitHub renders as its text
    private static readonly Regex _headingLink = new(@"!?\[(?<text>[^\]]*)\]\([^)]*\)", RegexOptions.CultureInvariant);

    // An ATX heading, with its optional closing #s left out of the text
    private static readonly Regex _heading = new(@"^ {0,3}#{1,6}[ \t]+(?<text>.*?)(?:[ \t]+#+)?[ \t]*$", RegexOptions.CultureInvariant);

    // GitHub keeps letters, marks, numbers, connector punctuation such as _, spaces, and hyphens, and drops everything else
    private static readonly Regex _anchorDrop = new(@"[^\p{L}\p{M}\p{N}\p{Pc} \-]", RegexOptions.CultureInvariant);

    private MarkdownDocument(string path, IReadOnlyList<MarkdownLink> links, IReadOnlySet<string> anchors)
    {
        Path = path;
        Links = links;
        Anchors = anchors;
    }

    public string Path { get; }

    public IReadOnlyList<MarkdownLink> Links { get; }

    public IReadOnlySet<string> Anchors { get; }

    public static MarkdownDocument Load(string path)
    {
        var links = new List<MarkdownLink>();
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        var anchorCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        string? fence = null;
        var lines = File.ReadAllLines(path);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.TrimStart(' ');
            if (fence is not null)
            {
                // A closing fence uses the same character, is at least as long as the opening one, and has no info string
                var closing = FenceLength(trimmed, fence[0]);
                if (closing >= fence.Length && string.IsNullOrWhiteSpace(trimmed.Substring(closing)))
                {
                    fence = null;
                }

                continue;
            }

            var opening = trimmed.Length > 0 && (trimmed[0] == '`' || trimmed[0] == '~') ? FenceLength(trimmed, trimmed[0]) : 0;
            // A backtick fence's info string can't hold a backtick; otherwise the line is inline code
            if (opening >= 3 && (trimmed[0] == '~' || trimmed.IndexOf('`', opening) < 0))
            {
                fence = trimmed.Substring(0, opening);
                continue;
            }

            var heading = _heading.Match(line);
            if (heading.Success)
            {
                anchors.Add(CreateAnchor(heading.Groups["text"].Value, anchorCounts));
            }

            var reference = _referenceDefinition.Match(line);
            if (reference.Success)
            {
                links.Add(new MarkdownLink(reference.Groups["target"].Value, index + 1));
            }

            foreach (Match link in _inlineLink.Matches(_inlineCode.Replace(line, string.Empty)))
            {
                links.Add(new MarkdownLink(link.Groups["target"].Value, index + 1));
            }
        }

        return new MarkdownDocument(path, links, anchors);
    }

    // Counts the run of fence characters at the start of a line
    private static int FenceLength(string line, char fenceCharacter)
    {
        var length = 0;
        while (length < line.Length && line[length] == fenceCharacter)
        {
            length++;
        }

        return length;
    }

    // Lowercases the heading's rendered text, drops punctuation, and turns spaces into hyphens. A repeated anchor gets -1, -2, and so on.
    private static string CreateAnchor(string headingText, Dictionary<string, int> anchorCounts)
    {
        // A link in a heading renders as its text; the backticks of inline code are punctuation, so they're dropped below
        var text = _headingLink.Replace(headingText, "${text}");
        var anchor = _anchorDrop.Replace(text.Trim().ToLowerInvariant(), string.Empty).Replace(' ', '-');
        if (anchorCounts.TryGetValue(anchor, out var count))
        {
            anchorCounts[anchor] = count + 1;

            return anchor + "-" + count.ToString(CultureInfo.InvariantCulture);
        }

        anchorCounts[anchor] = 1;

        return anchor;
    }
}
