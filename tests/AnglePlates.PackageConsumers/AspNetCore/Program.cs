using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AspNetCore.Templates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AspNetCore;

internal static class Program
{
    private const string _contentType = "text/html; charset=utf-8";

    private static async Task<int> Main()
    {
        // The small page fits in one flush; the large one passes the flush threshold and Kestrel's response buffer many times
        var small = new PageModel("Small & simple", Rows(3).ToList());
        var large = new PageModel("Large <page>", Rows(5000).ToList());
        var endless = new PageModel("Endless", Rows(int.MaxValue));
        var stopped = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.MapGet("/small", (HttpContext context) => RenderAsync(context, small));
        app.MapGet("/large", (HttpContext context) => RenderAsync(context, large));
        app.MapGet("/render", (HttpContext context) =>
        {
            // Render writes to the response without flushing; ASP.NET Core flushes when the endpoint returns
            context.Response.ContentType = _contentType;
            PageUtf8Template.Default.Render(large, context.Response.BodyWriter);
            return Task.CompletedTask;
        });
        app.MapGet("/endless", async (HttpContext context) =>
        {
            // Only a disconnect can end this render
            try
            {
                await RenderAsync(context, endless);
                stopped.TrySetResult(new InvalidOperationException("The endless page finished rendering."));
            }
            catch (Exception exception)
            {
                stopped.TrySetResult(exception);
            }
        });

        await app.StartAsync();
        var failures = new List<string>();
        using (var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) })
        {
            await CheckAsync(client, "/small", small, failures);
            await CheckAsync(client, "/large", large, failures);
            await CheckAsync(client, "/render", large, failures);
            await DisconnectAsync(client, stopped.Task, failures);
        }

        await app.StopAsync();
        if (failures.Count > 0)
        {
            Console.Error.WriteLine("AspNetCore failed: " + string.Join(" ", failures));
            return 1;
        }

        Console.WriteLine("AspNetCore passed.");
        return 0;
    }

    private static Task RenderAsync(HttpContext context, PageModel model)
    {
        context.Response.ContentType = _contentType;

        return PageUtf8Template.Default.RenderAsync(model, context.Response.BodyWriter, context.RequestAborted).AsTask();
    }

    private static async Task CheckAsync(HttpClient client, string path, PageModel model, List<string> failures)
    {
        var expected = new ArrayBufferWriter<byte>();
        PageUtf8Template.Default.Render(model, expected);

        using var response = await client.GetAsync(path);
        var actual = await response.Content.ReadAsByteArrayAsync();
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.ToString() != _contentType)
        {
            failures.Add($"{path} returned {(int)response.StatusCode} with content type '{response.Content.Headers.ContentType}'.");
        }
        else if (!actual.AsSpan().SequenceEqual(expected.WrittenSpan))
        {
            failures.Add($"{path} returned {actual.Length} bytes that don't match the {expected.WrittenCount} bytes Render wrote.");
        }
    }

    private static async Task DisconnectAsync(HttpClient client, Task<Exception> stopped, List<string> failures)
    {
        // Read the start of the page, then drop the connection mid-render
        using (var response = await client.GetAsync("/endless", HttpCompletionOption.ResponseHeadersRead))
        {
            await using var body = await response.Content.ReadAsStreamAsync();
            var start = new byte[1024];
            await body.ReadExactlyAsync(start);
        }

        var finished = await Task.WhenAny(stopped, Task.Delay(TimeSpan.FromSeconds(30)));
        if (finished != stopped)
        {
            failures.Add("/endless kept rendering after the client disconnected.");
        }
        else if (await stopped is not OperationCanceledException)
        {
            failures.Add($"/endless stopped with {(await stopped).GetType().Name} instead of an OperationCanceledException.");
        }
    }

    private static IEnumerable<string> Rows(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return $"Row {i} & more";
        }
    }
}
