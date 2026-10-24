using System;

namespace SlusserLabs.AnglePlates;

// Builds the exception for an enum value that a switch doesn't handle
internal static class UnexpectedValue
{
    // Every switch handles every value of its enum, so no test can reach a call to this. These calls are the one
    // accepted gap in generator coverage, and routing them through here keeps the gap in one place.
    public static ArgumentOutOfRangeException Exception<TEnum>(TEnum value, string parameterName) where TEnum : struct, Enum
    {
        return new ArgumentOutOfRangeException(parameterName, value, $"Unexpected {typeof(TEnum).Name} value.");
    }
}
