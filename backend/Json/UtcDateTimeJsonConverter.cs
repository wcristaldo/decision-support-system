using System.Text.Json;
using System.Text.Json.Serialization;

namespace DecisionSupportAPI.Json;

/// <summary>
/// Serializa los DateTime con la "Z" de UTC cuando vienen con Kind=Unspecified.
/// En este sistema todas las fechas con hora se guardan en UTC (DateTime.UtcNow), pero Npgsql
/// devuelve las columnas "timestamp sin zona horaria" como Unspecified y System.Text.Json las
/// escribe sin zona: el navegador las toma como hora local y las muestra adelantadas (en Paraguay,
/// 3 horas). Las fechas "solo día" (hora 00:00:00) se dejan tal cual para que sigan mostrando el
/// mismo día, y los valores que ya son UTC o tienen zona no se tocan.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        if (value.Kind == DateTimeKind.Unspecified && value.TimeOfDay != TimeSpan.Zero)
            value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        writer.WriteStringValue(value);
    }
}
