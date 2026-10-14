using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using MyApp;
using MyApp.Templates;

namespace AspNetCore;

// The README's ASP.NET Core example, copied line for line into a method so it compiles against the packed generator. The
// README writes it as top-level statements, which can't sit next to this app's Main, and nothing calls it, because
// app.Run() would block until shutdown.
internal static class ReadmeExample
{
    public static void Run(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        var model = new GreetingModel
        {
            Name = "Ada & Grace",
            Message = "Welcome!"
        };

        app.MapGet("/greeting", async (HttpContext context) =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";

            await GreetingUtf8Template.Default.RenderAsync(model, context.Response.BodyWriter, context.RequestAborted);
        });

        app.Run();
    }
}
