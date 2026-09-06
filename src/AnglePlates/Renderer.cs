using System;

namespace SlusserLabs.AnglePlates;

[Flags]
internal enum Renderer
{
    None = 0,
    Utf8 = 1 << 0,
    Text = 1 << 1,
}
