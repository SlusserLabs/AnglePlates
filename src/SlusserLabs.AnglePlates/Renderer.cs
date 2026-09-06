using System;
using System.Collections.Generic;
using System.Text;

namespace SlusserLabs.AnglePlates
{
    [Flags]
    public enum Renderer
    {
        None = 0,
        Utf8 = 1 << 0,
        Text = 1 << 1,
    }
}
