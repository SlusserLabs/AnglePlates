namespace MyApp.Templates;

// The hand-written half of the generated OrderTextTemplate
internal sealed partial class OrderTextTemplate
{
    private OrderLineTextTemplate? _line;

    // One child per parent instance, created with the parent's options
    private OrderLineTextTemplate Line => _line ??= new(Options);
}
