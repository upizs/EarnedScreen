using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EarnedScreen.Core;

public sealed class NotionTask
{
    public string PageId { get; set; } = "";
    public string Title { get; set; } = "";
}

public sealed class NotionFetchResult
{
    /// <summary>True if Notion was reached and answered successfully.</summary>
    public bool Available { get; set; }

    /// <summary>Populated when <see cref="Available"/> is false (drives the "tasklist not found" note).</summary>
    public string? Error { get; set; }

    /// <summary>Today's open (not-complete) tasks.</summary>
    public List<NotionTask> Tasks { get; set; } = new();

    /// <summary>The Status option id representing "done", discovered from the schema (for write-back).</summary>
    public string? DoneOptionId { get; set; }
}

/// <summary>
/// Minimal Notion REST client (API version 2022-06-28) for the daily-tasks gateway.
/// Discovers the Status/title schema at runtime so it tolerates custom property/option names.
/// Every call is wrapped so a failure degrades to <see cref="NotionFetchResult.Available"/> = false
/// rather than blocking the user from earning a session.
/// </summary>
public sealed class NotionTasksClient : IDisposable
{
    private const string NotionVersion = "2022-06-28";
    private readonly HttpClient _http;

    public NotionTasksClient(HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = new Uri("https://api.notion.com/");
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<NotionFetchResult> GetTodayOpenTasksAsync(NotionSettings cfg, CancellationToken ct = default)
    {
        var result = new NotionFetchResult();

        if (!cfg.Enabled || string.IsNullOrWhiteSpace(cfg.Token) || string.IsNullOrWhiteSpace(cfg.TasksDatabaseId))
        {
            result.Available = false;
            result.Error = "Notion not configured";
            return result;
        }

        try
        {
            var schema = await GetSchemaAsync(cfg, ct);
            result.DoneOptionId = schema.DoneOptionId;

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var filter = new { filter = new { property = cfg.DueProperty, date = new { equals = today } } };

            using var req = NewRequest(HttpMethod.Post, $"v1/databases/{cfg.TasksDatabaseId}/query", cfg.Token);
            req.Content = JsonBody(filter);
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                result.Available = false;
                result.Error = $"Notion query failed ({(int)resp.StatusCode})";
                return result;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var page in results.EnumerateArray())
                {
                    if (!page.TryGetProperty("properties", out var props)) continue;
                    if (IsComplete(props, cfg.StatusProperty, schema.CompleteOptionIds)) continue;

                    var title = ExtractTitle(props, schema.TitleProperty);
                    result.Tasks.Add(new NotionTask
                    {
                        PageId = page.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
                        Title = string.IsNullOrWhiteSpace(title) ? "(untitled task)" : title,
                    });
                }
            }

            result.Available = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Available = false;
            result.Error = ex.Message;
            return result;
        }
    }

    /// <summary>Sets the task's Status to the complete-group option. Best-effort; returns success.</summary>
    public async Task<bool> MarkTaskDoneAsync(NotionSettings cfg, string pageId, string? doneOptionId, CancellationToken ct = default)
    {
        try
        {
            doneOptionId ??= (await GetSchemaAsync(cfg, ct)).DoneOptionId;

            object statusValue = doneOptionId is not null
                ? new { status = new { id = doneOptionId } }
                : new { status = new { name = "Done" } };

            var body = new { properties = new Dictionary<string, object> { [cfg.StatusProperty] = statusValue } };

            using var req = NewRequest(HttpMethod.Patch, $"v1/pages/{pageId}", cfg.Token);
            req.Content = JsonBody(body);
            using var resp = await _http.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public sealed record Schema(string TitleProperty, HashSet<string> CompleteOptionIds, string? DoneOptionId);

    private async Task<Schema> GetSchemaAsync(NotionSettings cfg, CancellationToken ct)
    {
        using var req = NewRequest(HttpMethod.Get, $"v1/databases/{cfg.TasksDatabaseId}", cfg.Token);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));

        return ParseSchema(doc.RootElement, cfg);
    }

    /// <summary>Pulled out for unit testing against a captured database response.</summary>
    public static Schema ParseSchemaForTest(JsonElement databaseRoot, NotionSettings cfg) => ParseSchema(databaseRoot, cfg);

    private static Schema ParseSchema(JsonElement databaseRoot, NotionSettings cfg)
    {
        var properties = databaseRoot.GetProperty("properties");

        // Title property: prefer the configured name, else the property whose type is "title".
        var titleProp = cfg.TitleProperty;
        if (!properties.TryGetProperty(titleProp, out _))
        {
            foreach (var p in properties.EnumerateObject())
            {
                if (p.Value.TryGetProperty("type", out var t) && t.GetString() == "title")
                {
                    titleProp = p.Name;
                    break;
                }
            }
        }

        var completeIds = new HashSet<string>(StringComparer.Ordinal);
        string? doneOptionId = null;

        if (properties.TryGetProperty(cfg.StatusProperty, out var statusProp)
            && statusProp.TryGetProperty("status", out var statusObj))
        {
            // Status "groups" mark which options count as complete (group named "Complete"/"Done").
            if (statusObj.TryGetProperty("groups", out var groups))
            {
                foreach (var g in groups.EnumerateArray())
                {
                    var gname = g.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!(gname.Equals("Complete", StringComparison.OrdinalIgnoreCase)
                          || gname.Equals("Done", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    if (g.TryGetProperty("option_ids", out var optionIds))
                    {
                        foreach (var oid in optionIds.EnumerateArray())
                        {
                            var id = oid.GetString();
                            if (id is null) continue;
                            completeIds.Add(id);
                            doneOptionId ??= id;
                        }
                    }
                }
            }

            // Fallback: an option literally named "Done".
            if (doneOptionId is null && statusObj.TryGetProperty("options", out var opts))
            {
                foreach (var o in opts.EnumerateArray())
                {
                    var name = o.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
                    if (name.Equals("Done", StringComparison.OrdinalIgnoreCase))
                    {
                        var id = o.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                        if (id is not null) { completeIds.Add(id); doneOptionId = id; }
                    }
                }
            }
        }

        return new Schema(titleProp, completeIds, doneOptionId);
    }

    /// <summary>True if the page's Status is in the complete group. Unset status counts as open.</summary>
    public static bool IsComplete(JsonElement props, string statusProp, HashSet<string> completeOptionIds)
    {
        if (!props.TryGetProperty(statusProp, out var sp)) return false;
        if (!sp.TryGetProperty("status", out var st) || st.ValueKind == JsonValueKind.Null) return false;
        var id = st.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        return id is not null && completeOptionIds.Contains(id);
    }

    /// <summary>Joins a title property's rich-text into a plain string.</summary>
    public static string ExtractTitle(JsonElement props, string titleProp)
    {
        if (!props.TryGetProperty(titleProp, out var tp)) return "";
        if (!tp.TryGetProperty("title", out var arr) || arr.ValueKind != JsonValueKind.Array) return "";

        var sb = new StringBuilder();
        foreach (var t in arr.EnumerateArray())
            if (t.TryGetProperty("plain_text", out var pt))
                sb.Append(pt.GetString());
        return sb.ToString().Trim();
    }

    private static HttpRequestMessage NewRequest(HttpMethod method, string url, string token)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("Notion-Version", NotionVersion);
        return req;
    }

    private static StringContent JsonBody(object value)
        => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    public void Dispose() => _http.Dispose();
}
