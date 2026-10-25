using System;
using System.Globalization;
using Newtonsoft.Json;

/// <summary>Modern tracker names with stable legacy serialized values and wire conventions.</summary>
public static class TrackerBackend
{
    public static ClosedLoopMode Normalize(ClosedLoopMode mode) => mode == ClosedLoopMode.Rekkefly ? ClosedLoopMode.Kinefly : mode == ClosedLoopMode.Hejjetrac ? ClosedLoopMode.FicTrac : mode;
    public static bool IsFlight(ClosedLoopMode mode) => Normalize(mode) == ClosedLoopMode.Kinefly;
    public static bool IsWalking(ClosedLoopMode mode) => Normalize(mode) == ClosedLoopMode.FicTrac;
    public static string DisplayName(ClosedLoopMode mode) => IsFlight(mode) ? "Rekkefly" : IsWalking(mode) ? "HejjéTrac" : mode == ClosedLoopMode.Tirbala ? "Tirbala" : mode.ToString();
    public static string CompatibilityLabel(ClosedLoopMode mode) => mode == ClosedLoopMode.Kinefly ? "Legacy Kinefly setting; compatible flight input" : mode == ClosedLoopMode.FicTrac ? "Legacy FicTrac setting; compatible walking input" : "";
    public static bool TryParse(string value, out ClosedLoopMode mode)
    {
        if (string.Equals(value, "HejjéTrac", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "hejjetrak", StringComparison.OrdinalIgnoreCase))
        { mode = ClosedLoopMode.Hejjetrac; return true; }
        return Enum.TryParse(value, true, out mode) && Enum.IsDefined(typeof(ClosedLoopMode), mode);
    }
    public static ClosedLoopMode Parse(string value)
    {
        if (TryParse(value, out var mode)) return mode;
        throw new ArgumentException("Unknown tracker backend '" + value + "'. Use Hejjetrac, Rekkefly, FicTrac, Kinefly or Tirbala.");
    }
}

public sealed class TrackerBackendConverter : JsonConverter
{
    public override bool CanConvert(Type type) => type == typeof(ClosedLoopMode);
    public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.String && reader.TokenType != JsonToken.Integer)
            throw new JsonSerializationException("Tracker backend must be a supported name or legacy integer.");
        try { return TrackerBackend.Parse(Convert.ToString(reader.Value, CultureInfo.InvariantCulture)); }
        catch (ArgumentException error) { throw new JsonSerializationException(error.Message, error); }
    }
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => writer.WriteValue(value.ToString());
}
