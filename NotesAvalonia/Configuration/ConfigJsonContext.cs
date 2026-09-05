using System.Text.Json.Serialization;

namespace NotesAvalonia.Configuration;

/// <summary>Source-generated JSON metadata for the local config file (AOT-safe).</summary>
[JsonSerializable(typeof(ConfigData))]
public partial class ConfigJsonContext : JsonSerializerContext
{
}
