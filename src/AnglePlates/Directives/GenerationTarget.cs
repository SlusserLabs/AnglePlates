using System;

namespace SlusserLabs.AnglePlates;

// The renderer classes a template generates; the names match the TextTemplate and Utf8Template base classes
[Flags]
internal enum GenerationTarget
{
    None = 0,
    Utf8 = 1 << 0,
    Text = 1 << 1,
}
