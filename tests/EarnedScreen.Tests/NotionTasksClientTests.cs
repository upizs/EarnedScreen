using System.Net;
using System.Text;
using System.Text.Json;
using EarnedScreen.Core;

namespace EarnedScreen.Tests;

public sealed class NotionTasksClientTests
{
    private const string SchemaJson = """
    {
      "properties": {
        "Task name": { "id": "title", "type": "title", "title": {} },
        "Due":       { "id": "due",   "type": "date",  "date": {} },
        "Status": {
          "id": "stat", "type": "status",
          "status": {
            "options": [
              { "id": "o1", "name": "To-do" },
              { "id": "o2", "name": "In progress" },
              { "id": "o3", "name": "Done" }
            ],
            "groups": [
              { "id": "g1", "name": "To-do",       "option_ids": ["o1"] },
              { "id": "g2", "name": "In progress", "option_ids": ["o2"] },
              { "id": "g3", "name": "Complete",    "option_ids": ["o3"] }
            ]
          }
        }
      }
    }
    """;

    private const string QueryJson = """
    {
      "results": [
        { "id": "p1", "properties": {
            "Task name": { "type": "title", "title": [{ "plain_text": "Write report" }] },
            "Status":    { "type": "status", "status": { "id": "o1", "name": "To-do" } } } },
        { "id": "p2", "properties": {
            "Task name": { "type": "title", "title": [{ "plain_text": "Done task" }] },
            "Status":    { "type": "status", "status": { "id": "o3", "name": "Done" } } } },
        { "id": "p3", "properties": {
            "Task name": { "type": "title", "title": [{ "plain_text": "No status task" }] },
            "Status":    { "type": "status", "status": null } } }
      ]
    }
    """;

    private static NotionSettings EnabledConfig() => new()
    {
        Enabled = true,
        Token = "secret_test",
        TasksDatabaseId = "db123",
    };

    [Fact]
    public async Task GetTodayOpenTasks_returns_only_open_tasks()
    {
        using var client = new NotionTasksClient(new FakeHandler(SchemaJson, QueryJson));

        var result = await client.GetTodayOpenTasksAsync(EnabledConfig());

        Assert.True(result.Available);
        Assert.Equal("o3", result.DoneOptionId);
        Assert.Equal(2, result.Tasks.Count);
        Assert.Contains(result.Tasks, t => t.Title == "Write report");
        Assert.Contains(result.Tasks, t => t.Title == "No status task"); // unset status counts as open
        Assert.DoesNotContain(result.Tasks, t => t.Title == "Done task");
    }

    [Fact]
    public async Task Disabled_or_unconfigured_returns_unavailable()
    {
        using var client = new NotionTasksClient(new FakeHandler(SchemaJson, QueryJson));

        var disabled = await client.GetTodayOpenTasksAsync(new NotionSettings { Enabled = false });
        Assert.False(disabled.Available);

        var noToken = await client.GetTodayOpenTasksAsync(new NotionSettings { Enabled = true, TasksDatabaseId = "x" });
        Assert.False(noToken.Available);
    }

    [Fact]
    public async Task Http_failure_degrades_to_unavailable()
    {
        using var client = new NotionTasksClient(new FailingHandler());
        var result = await client.GetTodayOpenTasksAsync(EnabledConfig());
        Assert.False(result.Available);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ParseSchema_finds_title_and_complete_group()
    {
        using var doc = JsonDocument.Parse(SchemaJson);
        var schema = NotionTasksClient.ParseSchemaForTest(doc.RootElement, EnabledConfig());

        Assert.Equal("Task name", schema.TitleProperty);
        Assert.Contains("o3", schema.CompleteOptionIds);
        Assert.Equal("o3", schema.DoneOptionId);
    }

    [Fact]
    public void ExtractTitle_joins_rich_text_segments()
    {
        const string json = """
        { "Task name": { "type": "title", "title": [
            { "plain_text": "Hello " }, { "plain_text": "world" } ] } }
        """;
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Hello world", NotionTasksClient.ExtractTitle(doc.RootElement, "Task name"));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _schema;
        private readonly string _query;
        public FakeHandler(string schema, string query) { _schema = schema; _query = query; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // GET => database schema; POST => query results.
            var body = request.Method == HttpMethod.Get ? _schema : _query;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("network down");
    }
}
