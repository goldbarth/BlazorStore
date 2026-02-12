using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArcFlow.Features.YouTubePlayer.ImportExport;

public static class ExportSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(ExportEnvelopeV1 envelope)
    {
        return JsonSerializer.Serialize(envelope, Options);
    }

    public static ExportEnvelopeV1? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ExportEnvelopeV1>(json, Options);
    }
}
