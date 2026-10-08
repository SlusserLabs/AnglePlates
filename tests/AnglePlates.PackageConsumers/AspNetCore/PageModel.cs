using System.Collections.Generic;

namespace AspNetCore;

internal sealed class PageModel
{
    public PageModel(string title, IEnumerable<string> rows)
    {
        Title = title;
        Rows = rows;
    }

    public string Title { get; }

    public IEnumerable<string> Rows { get; }
}
