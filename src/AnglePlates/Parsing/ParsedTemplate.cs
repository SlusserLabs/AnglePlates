using System.Collections.Generic;

namespace SlusserLabs.AnglePlates;

// What the parser found in one template: its directives, its body tokens, and any unclosed blocks
internal sealed class ParsedTemplate
{
    public ParsedTemplate(string text, IReadOnlyList<DirectiveToken> directives, IReadOnlyList<Token> tokens, IReadOnlyList<TemplateError> errors, int bodyStart)
    {
        Text = text;
        Directives = directives;
        Tokens = tokens;
        Errors = errors;
        BodyStart = bodyStart;
    }

    // Every span in the directives, tokens, and errors points into this text
    public string Text { get; }

    public IReadOnlyList<DirectiveToken> Directives { get; }

    public IReadOnlyList<Token> Tokens { get; }

    public IReadOnlyList<TemplateError> Errors { get; }

    // Where the first line that isn't a directive starts
    public int BodyStart { get; }
}
