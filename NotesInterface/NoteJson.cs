using System.Text.Json;

namespace Notes.Interface;

/// <summary>
/// Shared System.Text.Json options for note payloads (wire format) and the local config file.
/// Property names stay PascalCase exactly as declared - the same shape Newtonsoft produced - so
/// existing payloads and config.json files remain readable. Reading is case-insensitive for
/// robustness against older servers/clients. Uses the source-generated resolver so serialization
/// also works when reflection-based STJ is disabled (NativeAOT publish).
/// </summary>
public static class NoteJson
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = NoteJsonContext.Default
    };
}
