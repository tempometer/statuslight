using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace StatusLight.Tests;

/// <summary>
/// The published JSON Schema is the cross-language contract: both examples must
/// validate, and a producer-facing typo must fail here, in CI, without running
/// any renderer. The YAML→JSON conversion applies standard implicit typing
/// (plain scalars become numbers/booleans/null; quoted scalars stay strings),
/// matching what producers like PyYAML's safe_load hand to their validators.
/// </summary>
public sealed class SchemaTests
{
    private static readonly JsonSchema Schema = JsonSchema.FromFile(
        Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "schema", "statuslight.schema.json"));

    private static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "schema")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root with schema/ not found");
    }

    private static EvaluationResults ValidateYaml(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        var element = JsonSerializer.SerializeToElement(ToJson(stream.Documents[0].RootNode));
        return Schema.Evaluate(element, new EvaluationOptions { OutputFormat = OutputFormat.List });
    }

    private static JsonNode? ToJson(YamlNode? node) => node switch
    {
        null => null,
        YamlScalarNode { Style: ScalarStyle.Plain, Value: null or "" or "null" or "Null" or "NULL" or "~" } => null,
        YamlScalarNode { Style: ScalarStyle.Plain } s when long.TryParse(s.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) => JsonValue.Create(l),
        YamlScalarNode { Style: ScalarStyle.Plain } s when IsPlainBool(s.Value) => JsonValue.Create(bool.Parse(s.Value)),
        YamlScalarNode { Style: ScalarStyle.Plain } s when double.TryParse(s.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => JsonValue.Create(d),
        YamlScalarNode s => JsonValue.Create(s.Value),
        YamlMappingNode m => new JsonObject(m.Select(c => new KeyValuePair<string, JsonNode?>(Key(c.Key), ToJson(c.Value)))),
        YamlSequenceNode q => new JsonArray(q.Children.Select(ToJson).ToArray()),
        _ => throw new InvalidOperationException($"unexpected YAML node {node.GetType()}")
    };

    private static bool IsPlainBool(string? value) =>
        value is "true" or "True" or "TRUE" or "false" or "False" or "FALSE";

    private static string Key(YamlNode key) => ((YamlScalarNode)key).Value ?? "";

    private static string Describe(EvaluationResults results) => string.Join("; ",
        (results.Details ?? [])
            .Where(d => d.Errors is not null)
            .Select(d => $"{d.InstanceLocation}: {JsonSerializer.Serialize(d.Errors)}"));

    [Fact]
    public void Schema_DeployExample_IsValid()
    {
        var yaml = File.ReadAllText(Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "examples", "deploy.yml"));

        var results = ValidateYaml(yaml);

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Schema_TrafficBoardExample_IsValid()
    {
        var yaml = File.ReadAllText(Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "examples", "traffic-board.yml"));

        var results = ValidateYaml(yaml);

        Assert.True(results.IsValid, Describe(results));
    }

    [Fact]
    public void Schema_BadTheme_IsInvalid()
    {
        var results = ValidateYaml("""
            board:
              title: T
              theme: blue
            sections:
              - name: S
                updated: "2026-11-18 09:12"
                items: [{ name: A, state: green }]
            """);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Schema_MissingBoardTitle_IsInvalid()
    {
        var results = ValidateYaml("""
            board:
              description: no title
            sections:
              - name: S
                updated: "2026-11-18 09:12"
                items: [{ name: A, state: green }]
            """);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Schema_DateOnlyUpdated_IsInvalid()
    {
        var results = ValidateYaml("""
            board:
              title: T
            sections:
              - name: S
                updated: "2026-11-18"
                items: [{ name: A, state: green }]
            """);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Schema_LinkWithoutHref_IsInvalid()
    {
        var results = ValidateYaml("""
            board:
              title: T
            sections:
              - name: S
                updated: "2026-11-18 09:12"
                items: [{ name: A, state: green }]
                links: [{ label: docs }]
            """);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Schema_TagTooLong_IsInvalid()
    {
        var results = ValidateYaml("""
            board:
              title: T
            sections:
              - name: S
                updated: "2026-11-18 09:12"
                items:
                  - { tag: VERYLONGTAGNAME123, note: x }
            """);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Schema_LegacyUnknownKey_IsInvalid()
    {
        var results = ValidateYaml("""
            title: T
            items:
              - name: A
                state: green
                notes: typo
            """);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Schema_EmptySections_IsInvalid()
    {
        var results = ValidateYaml("""
            board:
              title: T
            sections: []
            """);

        Assert.False(results.IsValid);
    }
}
