using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using OllamaModeller.Models;

namespace OllamaModeller.Services;

public class OllamaApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:11434";

    public OllamaApiService()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<OllamaModel>> GetModelsAsync()
    {
        var response = await _httpClient.GetAsync("/api/tags");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var models = new List<OllamaModel>();
        foreach (var item in doc.RootElement.GetProperty("models").EnumerateArray())
        {
            models.Add(new OllamaModel
            {
                Name = item.GetProperty("name").GetString() ?? "",
                Size = FormatSize(item.GetProperty("size").GetInt64()),
                Modified = DateTime.TryParse(item.TryGetProperty("modified_at", out var mod) ? mod.GetString() : null, out var dt) ? dt : DateTime.MinValue
            });
        }
        return models;
    }

    public async Task<List<ModelStatistics>> GetRunningModelsAsync()
    {
        var response = await _httpClient.GetAsync("/api/ps");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var models = new List<ModelStatistics>();
        foreach (var item in doc.RootElement.GetProperty("models").EnumerateArray())
        {
            models.Add(new ModelStatistics
            {
                Name = item.GetProperty("name").GetString() ?? "",
                CpuPercent = item.GetProperty("cpu_percent").GetDouble(),
                MemoryBytes = item.GetProperty("memory").GetInt64(),
                StartedAt = DateTime.TryParse(item.TryGetProperty("started_at", out var start) ? start.GetString() : null, out var dt) ? dt : DateTime.MinValue
            });
        }
        return models;
    }

    public async Task PullModelAsync(string modelName, IProgress<double> progress, CancellationToken ct)
    {
        var content = new StringContent(JsonSerializer.Serialize(new { name = modelName }), System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/api/pull", content, ct);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line != null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("progress", out var prog))
                    {
                        progress.Report(prog.GetDouble());
                    }
                }
                catch { }
            }
        }
    }

    public async Task StopModelAsync(string modelName)
    {
        var content = new StringContent(JsonSerializer.Serialize(new { name = modelName }), System.Text.Encoding.UTF8, "application/json");
        await _httpClient.PostAsync("/api/generate", content);
    }

    private static string FormatSize(long bytes)
    {
        double gb = bytes / 1_073_741_824.0;
        if (gb >= 1) return $"{gb:F1} GB";
        double mb = bytes / 1_048_576.0;
        return $"{mb:F0} MB";
    }

    public void Dispose() => _httpClient.Dispose();
}
