using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OllamaModeller.Services;

var handler = new CapturingHandler();
using var httpClient = new HttpClient(handler)
{
    BaseAddress = new Uri("http://localhost:11434")
};

using var service = new OllamaApiService();
var field = typeof(OllamaApiService).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("Could not find OllamaApiService._httpClient.");
field.SetValue(service, httpClient);

await service.StopModelAsync("llama3.2");

AssertEqual(HttpMethod.Post, handler.Request?.Method, "HTTP method");
AssertEqual("/api/generate", handler.Request?.RequestUri?.AbsolutePath, "request path");

using var document = JsonDocument.Parse(handler.Body ?? "");
var root = document.RootElement;
AssertEqual("llama3.2", root.GetProperty("model").GetString(), "model payload field");
AssertEqual(0, root.GetProperty("keep_alive").GetInt32(), "keep_alive payload field");

Console.WriteLine("StopModelAsync sends Ollama unload request.");

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}

sealed class CapturingHandler : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }
    public string? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        Body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        };
    }
}
