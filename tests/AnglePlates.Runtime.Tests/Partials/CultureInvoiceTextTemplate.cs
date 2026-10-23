namespace SlusserLabs.AnglePlates.Runtime.Tests.Templates;

// The hand-written half of the generated CultureInvoiceTextTemplate. Coverage counts it with the emitted code it completes.
internal sealed partial class CultureInvoiceTextTemplate
{
    private CultureInvoiceLineTextTemplate? _line;

    // One child per parent instance, created with the parent's options so it inherits the culture
    private CultureInvoiceLineTextTemplate Line => _line ??= new(Options);
}
