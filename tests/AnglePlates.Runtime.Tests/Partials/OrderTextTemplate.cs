namespace SlusserLabs.AnglePlates.Runtime.Tests.Templates;

// The hand-written half of the generated OrderTextTemplate. Coverage counts it with the emitted code it completes.
internal sealed partial class OrderTextTemplate
{
    private OrderLineTextTemplate? _line;

    // One child per parent instance, created with the parent's options
    private OrderLineTextTemplate Line => _line ??= new(Options);
}
