using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Core.State;

namespace Core.Save;

public enum LoadStatus
{
    Ok,
    Corrupt,
    Incompatible,
}

public sealed record DecodeResult(LoadStatus Status, GameState? State, string? Error = null);

/// <summary>
/// Run save format: <c>{ "schema_version": N, "checksum": sha256(state bytes), "state": {...} }</c>.
/// The checksum covers the exact bytes of the <c>state</c> value as written, so it needs no
/// JSON canonicalization.
/// </summary>
public static class SaveSerializer
{
    private const string SchemaVersionField = "schema_version";
    private const string ChecksumField = "checksum";
    private const string StateField = "state";

    private static readonly JsonSerializerOptions StateOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions HashOptions = new(StateOptions) { WriteIndented = false };

    public static byte[] Encode(GameState state)
    {
        byte[] stateBytes = JsonSerializer.SerializeToUtf8Bytes(state, StateOptions);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber(SchemaVersionField, state.SchemaVersion);
            writer.WriteString(ChecksumField, Sha256Hex(stateBytes));
            writer.WritePropertyName(StateField);
            writer.WriteRawValue(stateBytes, skipInputValidation: true);
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }

    public static DecodeResult Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new Utf8JsonReader(bytes);
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            int version = root.GetProperty(SchemaVersionField).GetInt32();
            string checksum = root.GetProperty(ChecksumField).GetString() ?? "";
            var stateElement = root.GetProperty(StateField);

            byte[] stateBytes = RawBytes(stateElement);
            if (!string.Equals(Sha256Hex(stateBytes), checksum, StringComparison.Ordinal))
                return new DecodeResult(LoadStatus.Corrupt, null, "Checksum mismatch.");

            if (version > GameState.CurrentSchemaVersion)
                return new DecodeResult(LoadStatus.Incompatible, null, $"Save schema {version} is newer than {GameState.CurrentSchemaVersion}.");

            var node = JsonNode.Parse(stateBytes)!;
            if (!SaveMigrations.TryMigrate(node, version, out string? migrationError))
                return new DecodeResult(LoadStatus.Incompatible, null, migrationError);

            var state = node.Deserialize<GameState>(StateOptions)
                ?? throw new JsonException("State is null.");
            return new DecodeResult(LoadStatus.Ok, state);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return new DecodeResult(LoadStatus.Corrupt, null, e.Message);
        }
    }

    /// <summary>Stable hash of the full state, for golden tests and desync checks.</summary>
    public static string HashState(GameState state) =>
        Sha256Hex(JsonSerializer.SerializeToUtf8Bytes(state, HashOptions));

    private static byte[] RawBytes(JsonElement element) =>
        System.Text.Encoding.UTF8.GetBytes(element.GetRawText());

    private static string Sha256Hex(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
