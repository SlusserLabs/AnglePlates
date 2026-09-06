// using SlusserLabs.AnglePlates;
using SlusserLabs.AnglePlates;
using System.Buffers;
using System.Text;
using WebApplication1.ViewModels;

namespace WebApplication1
{
    internal static class Program
    {
        //public static void Main(string[] args)
        //{
        //    string text = File.ReadAllText(@"C:\Users\jacob\Downloads\template.html");
        //    var parser = new TemplateParser(TemplateParserOptions.Default);
        //    var tokens = parser.Parse(text);

        //    var transpilerOptions = TemplateTranspilerOptions.Create(text, tokens.Where(t => t.Type == TokenType.Directive), TemplateParserOptions.Default.Whitespace);
        //    var transpiler = new Utf8TemplateTranspiler(transpilerOptions); //new TextTemplateTranspiler(transpilerOptions);

        //    var builder = new StringBuilder();
        //    using var writer = new StringWriter(builder);
        //    transpiler.Transpile(writer, text, tokens);
        //    writer.Flush();

        //    Console.WriteLine(builder.ToString());
        //}

        public static void Main(string[] args)
        {
            var viewModel = new HelloWorldViewModel
            {
                Title = "Hello, world!",
            };
            viewModel.Paragraphs.Add("Lorem ipsum dolor sit amet, consectetur adipiscing elit.");
            viewModel.Paragraphs.Add("Aenean at aliquam nulla, id convallis lorem.");

            var opts = new TemplateOptions();

            // var t = new WebApplication1.Templates.HelloWorldTemplateText()

            var template = new Templates.HelloWorldTextTemplate(opts);
            var builder = new StringBuilder();
            using var writer = new StringWriter(builder);
            template.Render(viewModel, writer);
            writer.Flush();

            Console.WriteLine(builder.ToString());
        }


        //public static void Main(string[] args)
        //{
        //    var builder = WebApplication.CreateSlimBuilder(args);
        //    var app = builder.Build();

        //    app.MapGet("", async context =>
        //    {
        //        context.Response.ContentType = "text/html charset=utf-8";
        //        var viewModel = new HelloWorldViewModel
        //        {
        //            Title = "Hello, world!",
        //        };
        //        viewModel.Paragraphs.Add("Lorem ipsum dolor sit amet, consectetur adipiscing elit.");
        //        viewModel.Paragraphs.Add("Aenean at aliquam nulla, id convallis lorem.");

        //        var template = new HelloWorldUtf8Template<HelloWorldViewModel>();
        //        await template.RenderAsync(context.Response.BodyWriter, viewModel);
        //    });

        //    app.Run();
        //}
    }
}
