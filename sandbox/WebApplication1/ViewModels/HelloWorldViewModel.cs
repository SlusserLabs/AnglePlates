namespace WebApplication1.ViewModels;

public class HelloWorldViewModel
{
    public string Title { get; set; } = default!;

    public List<string> Paragraphs { get; } = new List<string>();
}
