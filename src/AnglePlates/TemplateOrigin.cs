namespace SlusserLabs.AnglePlates;

// How an AnglePlate item was added, from the AnglePlate metadata the targets file puts on its AdditionalFiles
internal enum TemplateOrigin
{
    None, // Not an AnglePlates file, so the generator never reads it
    Discovered,
    Explicit
}
