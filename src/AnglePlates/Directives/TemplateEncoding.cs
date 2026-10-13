namespace SlusserLabs.AnglePlates;

// The encodings #encode can pick. Each name matches the TemplateOptions preset the generated constructor falls back to,
// so the writer emits the name as it is; #encode none picks Unencoded.
internal enum TemplateEncoding
{
    Html,
    Unencoded,
    JavaScript,
    Url
}
