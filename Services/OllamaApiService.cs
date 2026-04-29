using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
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
        var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
        _httpClient = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromMinutes(10) };
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
        var modelsArray = doc.RootElement.GetProperty("models");
        var ollamaPsOutput = await GetOllamaPsOutputAsync();
        var psData = ParseOllamaPs(ollamaPsOutput);
        foreach (var item in modelsArray.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var model = item.TryGetProperty("model", out var m) ? m.GetString() ?? "" : "";
            var size = item.TryGetProperty("size", out var s) ? s.GetInt64() : 0L;
            var mem = item.TryGetProperty("memory", out var memProp) ? memProp.GetInt64() : 0L;
            var vram = item.TryGetProperty("size_vram", out var vramProp) ? vramProp.GetInt64() : 0L;
            var context = item.TryGetProperty("context_length", out var ctxProp) ? ctxProp.GetInt64() : 0L;
            var duration = item.TryGetProperty("duration", out var dur) ? dur.GetInt64() : 0L;
            string started = item.TryGetProperty("started_at", out var start) ? start.GetString() ?? "" : "";
            string until = item.TryGetProperty("until", out var u) ? u.GetString() ?? "" : "";
            var hasPsEntry = psData.TryGetValue(name, out var psEntry);
            var parsedUntil = DateTime.TryParse(until, out var untilDt) ? untilDt : (DateTime?)null;
            models.Add(new ModelStatistics
            {
                Name = name,
                Model = model,
                SizeBytes = size,
                Size = hasPsEntry && !string.IsNullOrWhiteSpace(psEntry.Size) ? psEntry.Size : FormatSize(size),
                CpuPercent = hasPsEntry ? psEntry.CpuPercent : 0,
                GpuPercent = hasPsEntry ? psEntry.GpuPercent : 0,
                GpuInfo = hasPsEntry ? psEntry.Processor : "",
                MemoryBytes = mem,
                Memory = FormatSize(mem),
                Context = context,
                ContextFormatted = hasPsEntry && !string.IsNullOrWhiteSpace(psEntry.Context) ? psEntry.Context : FormatContext(context),
                GpuMemoryBytes = vram,
                GpuMemory = FormatSize(vram),
                Duration = duration,
                DurationFormatted = FormatDuration(duration),
                Until = parsedUntil,
                RemainingTime = hasPsEntry && !string.IsNullOrWhiteSpace(psEntry.Until) ? psEntry.Until : GetRemainingTime(parsedUntil),
                StartedAt = DateTime.TryParse(started, out var dt) ? dt : DateTime.MinValue
            });
        }
        return models;
    }

    private async Task<string> GetOllamaPsOutputAsync()
    {
        try
        {
            var ollamaPath = FindOllamaPath();
            var psi = new ProcessStartInfo { FileName = ollamaPath, Arguments = "ps", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using var process = Process.Start(psi);
            if (process == null) return "";
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ollama ps error: {ex.Message}"); return ""; }
    }

    private string FindOllamaPath()
    {
        var paths = new[] { @"D:\Programs\Ollama\ollama.exe", @"C:\Users\VoV\AppData\Local\Programs\Ollama\ollama.exe", "ollama" };
        foreach (var p in paths)
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = p, Arguments = "--version", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
                using var process = Process.Start(psi);
                if (process != null) { process.WaitForExit(1000); if (process.ExitCode == 0) return p; }
            }
            catch { }
        }
        return "ollama";
    }

    private Dictionary<string, (string Size, double CpuPercent, double GpuPercent, string Processor, string Context, string Until)> ParseOllamaPs(string output)
    {
        var result = new Dictionary<string, (string, double, double, string, string, string)>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(output)) return result;
        var lines = output.Split('\n');
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("NAME")) continue;
            var nameMatch = Regex.Match(trimmedLine, @"^(\S+)");
            if (!nameMatch.Success) continue;
            var name = nameMatch.Groups[1].Value;
            var columns = Regex.Split(trimmedLine, @"\s{2,}");
            var size = columns.Length > 2 ? columns[2].Trim() : "";
            var processor = columns.Length > 3 ? columns[3].Trim() : "";
            var context = columns.Length > 4 ? columns[4].Trim() : "";
            var until = columns.Length > 5 ? columns[5].Trim() : "";
            var procMatch = Regex.Match(processor, @"(\d+(?:\.\d+)?)%/(\d+(?:\.\d+)?)%");
            double cpu = 0, gpu = 0;
            if (procMatch.Success)
            {
                double.TryParse(procMatch.Groups[1].Value.Replace("%", ""), out cpu);
                double.TryParse(procMatch.Groups[2].Value.Replace("%", ""), out gpu);
            }
            result[name] = (size, cpu, gpu, processor, context, until);
        }
        return result;
    }

    private static string FormatDuration(long nanoseconds)
    {
        var ts = TimeSpan.FromSeconds(nanoseconds / 1_000_000_000.0);
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    private static string FormatContext(long tokens)
    {
        return tokens.ToString();
    }

    public static string GetRemainingTime(DateTime? until)
    {
        if (!until.HasValue) return "";
        var remaining = until.Value - DateTime.Now;
        if (remaining.TotalSeconds <= 0) return "now";
        if (remaining.TotalMinutes < 1)
        {
            var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
            return $"{seconds} {Pluralize(seconds, "second")} from now";
        }
        if (remaining.TotalHours < 1)
        {
            var minutes = (int)Math.Ceiling(remaining.TotalMinutes);
            return $"{minutes} {Pluralize(minutes, "minute")} from now";
        }
        if (remaining.TotalDays < 1)
        {
            var hours = (int)Math.Ceiling(remaining.TotalHours);
            return $"{hours} {Pluralize(hours, "hour")} from now";
        }
        var days = (int)Math.Ceiling(remaining.TotalDays);
        return $"{days} {Pluralize(days, "day")} from now";
    }

    private static string Pluralize(int value, string unit) => value == 1 ? unit : $"{unit}s";

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

    public async Task RunModelAsync(string modelName, string prompt, CancellationToken ct = default)
    {
        var content = new StringContent(JsonSerializer.Serialize(new { model = modelName, prompt = prompt, stream = false }), System.Text.Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        using var response = await _httpClient.PostAsync("/api/generate", content, cts.Token);
        response.EnsureSuccessStatusCode();
    }

    public async Task StopModelAsync(string modelName)
    {
        var content = new StringContent(JsonSerializer.Serialize(new { model = modelName, keep_alive = 0 }), System.Text.Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("/api/generate", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteModelAsync(string modelName)
    {
        var content = new StringContent(JsonSerializer.Serialize(new { model = modelName }), System.Text.Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/delete") { Content = content };
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
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
