using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SlusserLabs.AnglePlates.Samples.HelloWorldApi;
using SlusserLabs.AnglePlates.Samples.HelloWorldApi.Templates;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", async (HttpContext context) =>
{
    var name = context.Request.Query["name"].ToString();
    var items = context.Request.Query.ContainsKey("empty") ? Array.Empty<ItemModel>() : new[]
    {
        new ItemModel { Title = "Write a model", IsComplete = true, Tags = new[] { "C#", "data & types" } },
        new ItemModel { Title = "Add an <html> template", Tags = new[] { "HTML", "encoding" } },
        new ItemModel { Title = "Stream the result" }
    };
    int completedItemCount = 0;
    foreach (var item in items)
    {
        if (item.IsComplete)
        {
            completedItemCount++;
        }
    }

    var model = new GreetingModel
    {
        Name = string.IsNullOrEmpty(name) ? "world" : name,
        Message = context.Request.Query["message"].ToString(),
        ProjectName = "My first template",
        Items = items,
        CompletedItemCount = completedItemCount
    };

    context.Response.ContentType = "text/html; charset=utf-8";
    await GreetingUtf8Template.Default.RenderAsync(model, context.Response.BodyWriter, context.RequestAborted);
});

app.Run();
