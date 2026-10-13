using System;

namespace SlusserLabs.AnglePlates.Runtime.Tests.Models;

// A [Flags] enum, so combined and undefined values show how flags convert
[Flags]
public enum Permissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
}
