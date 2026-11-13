using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickPatch.Actors.Application.Events;

/// <summary>JSON de los eventos en camelCase, como los esquemas de <c>quickpatch-kafka</c>.</summary>
public static class EventSerializer
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}