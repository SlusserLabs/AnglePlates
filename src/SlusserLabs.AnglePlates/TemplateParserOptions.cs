using System;
using System.Collections.Generic;
using System.Text;

namespace SlusserLabs.AnglePlates
{
    public struct TemplateParserOptions
    {
        public static TemplateParserOptions Default => _default;
        private static readonly TemplateParserOptions _default = new TemplateParserOptions
        {
            DirectiveStart = "#".ToCharArray(),
            VariableStart = "[[".ToCharArray(),
            VariableEnd = "]]".ToCharArray(),
            CodeStart = "[%".ToCharArray(),
            CodeEnd = "%]".ToCharArray(),
            CommentStart = "[*".ToCharArray(),
            CommentEnd = "*]".ToCharArray(),
            WhitespaceControl = "-".ToCharArray(),
            Whitespace = "\r\n\t ".ToCharArray()
        };

        // Leaving the door open for custom delimiters...
        public ReadOnlyMemory<char> DirectiveStart;
        public ReadOnlyMemory<char> VariableStart;
        public ReadOnlyMemory<char> VariableEnd;
        public ReadOnlyMemory<char> CodeStart;
        public ReadOnlyMemory<char> CodeEnd;
        public ReadOnlyMemory<char> CommentStart;
        public ReadOnlyMemory<char> CommentEnd;
        public ReadOnlyMemory<char> WhitespaceControl;
        public ReadOnlyMemory<char> Whitespace;
    }
}
