using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SlusserLabs.AnglePlates.PackageTests;

// Checks the packed README the way nuget.org renders it: relative links and images break there, and images only load from trusted hosts
internal static class ReadmeValidator
{
    // nuget.org's trusted image domains, from https://learn.microsoft.com/nuget/nuget-org/package-readme-on-nuget-org#allowed-domains-for-images-and-badges
    // github.com is missing on purpose; only its workflow badges are allowed, which IsTrustedImage checks separately
    private static readonly HashSet<string> _trustedImageHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "api.codacy.com",
        "api.codeclimate.com",
        "api.dependabot.com",
        "api.reuse.software",
        "api.travis-ci.com",
        "app.codacy.com",
        "app.deepsource.com",
        "avatars.githubusercontent.com",
        "badgen.net",
        "badges.gitter.im",
        "camo.githubusercontent.com",
        "caniuse.bitsofco.de",
        "cdn.jsdelivr.net",
        "cdn.syncfusion.com",
        "ci.appveyor.com",
        "circleci.com",
        "cloudback.it",
        "codecov.io",
        "codefactor.io",
        "coveralls.io",
        "dev.azure.com",
        "devpod.sh",
        "flat.badgen.net",
        "gitlab.com",
        "i.imgur.com",
        "img.shields.io",
        "infragistics.com",
        "isitmaintained.com",
        "media.githubusercontent.com",
        "opencollective.com",
        "raw.github.com",
        "raw.githubusercontent.com",
        "snyk.io",
        "sonarcloud.io",
        "travis-ci.com",
        "travis-ci.org",
        "user-images.githubusercontent.com"
    };

    // [text](target); link text may hold one level of nested brackets, such as the image in a linked badge
    private static readonly Regex _inlineLink = new(@"(?<!!)\[(?:[^\[\]]|\[[^\[\]]*\])*\]\(\s*<?([^)\s>]*)", RegexOptions.CultureInvariant);

    // ![alt](target), matched on its own so an image inside a link like [![badge](image)](link) gets checked too
    private static readonly Regex _inlineImage = new(@"!\[(?:[^\[\]]|\[[^\[\]]*\])*\]\(\s*<?([^)\s>]*)", RegexOptions.CultureInvariant);

    // ![alt][label], ![label][], and ![label], whose targets come from a reference definition
    private static readonly Regex _referenceImage = new(@"!\[([^\[\]]*)\](?:\[([^\[\]]*)\]|(?!\())", RegexOptions.CultureInvariant);

    // [label]: target
    private static readonly Regex _referenceDefinition = new(@"^ {0,3}\[([^\]]+)\]:\s*<?([^\s>]+)", RegexOptions.CultureInvariant | RegexOptions.Multiline);

    // <https://example.com>, which needs a scheme, so HTML tags don't match
    private static readonly Regex _autolink = new(@"<([A-Za-z][A-Za-z0-9+.-]*:[^\s<>]*)>", RegexOptions.CultureInvariant);

    // The src value may be double quoted, single quoted, or bare, and only one of the three groups captures
    private static readonly Regex _htmlImage = new(@"<img\b[^>]*?\ssrc\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'>]+))", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // Quoted the same three ways as an img src
    private static readonly Regex _htmlLink = new(@"<a\b[^>]*?\shref\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'>]+))", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // A run of backticks, then anything up to the same run; this is how Markdown ends a code span
    private static readonly Regex _codeSpan = new(@"(`+)[\s\S]*?(?<!`)\1(?!`)", RegexOptions.CultureInvariant);

    private static readonly Regex _htmlComment = new(@"<!--[\s\S]*?-->", RegexOptions.CultureInvariant);

    // Returns every problem, so one run lists them all
    public static IReadOnlyList<string> Validate(string readme)
    {
        var problems = new List<string>();
        var lines = readme.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        for (var index = 0; index < lines.Length; index++)
        {
            // Code blocks count too; a leftover TODO in an install snippet is still a leftover
            var trimmed = lines[index].TrimStart();
            if (trimmed.StartsWith("# TODO", StringComparison.Ordinal) || trimmed.StartsWith("TODO:", StringComparison.Ordinal))
            {
                problems.Add($"README line {index + 1} is a TODO: {lines[index]}");
            }
        }

        var prose = RemoveCode(lines);
        foreach (Match match in _inlineLink.Matches(prose))
        {
            CheckTarget(match.Groups[1].Value, false, problems);
        }

        foreach (Match match in _inlineImage.Matches(prose))
        {
            CheckTarget(match.Groups[1].Value, true, problems);
        }

        // A definition that any image uses is checked as an image, so it has to come from a trusted host
        var imageLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in _referenceImage.Matches(prose))
        {
            var label = string.IsNullOrWhiteSpace(match.Groups[2].Value) ? match.Groups[1].Value : match.Groups[2].Value;
            imageLabels.Add(NormalizeLabel(label));
        }

        foreach (Match match in _referenceDefinition.Matches(prose))
        {
            CheckTarget(match.Groups[2].Value, imageLabels.Contains(NormalizeLabel(match.Groups[1].Value)), problems);
        }

        foreach (Match match in _autolink.Matches(prose))
        {
            CheckTarget(match.Groups[1].Value, false, problems);
        }

        foreach (Match match in _htmlImage.Matches(prose))
        {
            CheckTarget(AttributeValue(match), true, problems);
        }

        foreach (Match match in _htmlLink.Matches(prose))
        {
            CheckTarget(AttributeValue(match), false, problems);
        }

        return problems;
    }

    // CommonMark matches reference labels case-insensitively, with runs of whitespace treated as one space
    private static string NormalizeLabel(string label) => Regex.Replace(label.Trim(), @"\s+", " ");

    // Returns whichever of the double-quoted, single-quoted, or bare groups matched
    private static string AttributeValue(Match match)
    {
        for (var group = 1; group <= 3; group++)
        {
            if (match.Groups[group].Success)
            {
                return match.Groups[group].Value;
            }
        }

        return string.Empty;
    }

    // Drops fenced code blocks, code spans, and HTML comments, which nuget.org doesn't render as links
    private static string RemoveCode(string[] lines)
    {
        var prose = new StringBuilder();
        var fenceCharacter = '\0';
        var fenceLength = 0;
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            var run = FenceRun(trimmed);
            if (fenceLength == 0)
            {
                if (run >= 3)
                {
                    fenceCharacter = trimmed[0];
                    fenceLength = run;
                }
                else
                {
                    prose.Append(line).Append('\n');
                }

                continue;
            }

            // A closing fence uses the same character, is at least as long as the opening one, and has nothing after it
            if (run >= fenceLength && trimmed[0] == fenceCharacter && string.IsNullOrWhiteSpace(trimmed[run..]))
            {
                fenceLength = 0;
            }
        }

        return _htmlComment.Replace(_codeSpan.Replace(prose.ToString(), string.Empty), string.Empty);
    }

    // Counts the backticks or tildes a line starts with
    private static int FenceRun(string trimmed)
    {
        if (trimmed.Length == 0 || (trimmed[0] != '`' && trimmed[0] != '~'))
        {
            return 0;
        }

        var run = 1;
        while (run < trimmed.Length && trimmed[run] == trimmed[0])
        {
            run++;
        }

        return run;
    }

    private static void CheckTarget(string target, bool isImage, List<string> problems)
    {
        if (isImage)
        {
            if (!IsTrustedImage(target))
            {
                problems.Add($"README image '{target}' isn't an https:// URL on a host nuget.org trusts.");
            }
        }
        else if (!target.StartsWith("https://", StringComparison.Ordinal) && !(target.StartsWith('#') && target.Length > 1))
        {
            problems.Add($"README link '{target}' isn't an absolute https:// URL or a #anchor; nuget.org doesn't resolve relative links.");
        }
    }

    private static bool IsTrustedImage(string target)
    {
        if (!target.StartsWith("https://", StringComparison.Ordinal) || !Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // github.com serves only workflow badges from the trusted list, such as /owner/repo/actions/workflows/ci.yml/badge.svg
        if (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsolutePath.Contains("/workflows/", StringComparison.Ordinal) && uri.AbsolutePath.EndsWith("/badge.svg", StringComparison.Ordinal);
        }

        return _trustedImageHosts.Contains(uri.Host);
    }
}
