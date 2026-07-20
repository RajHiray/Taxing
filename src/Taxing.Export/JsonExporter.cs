using System.Text.Json;
using System.Text.Json.Serialization;
using Taxing.Core.Models;

namespace Taxing.Export;

/// <summary>Serializes an <see cref="ItrResult"/> to indented JSON.</summary>
public static class JsonExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ToJson(ItrResult result) =>
        JsonSerializer.Serialize(result, Options);
}
