using System;

namespace SlusserLabs.AnglePlates;

[Flags]
internal enum GenerationTarget
{
    None = 0,
    Utf8 = 1 << 0,
    Text = 1 << 1,
}
