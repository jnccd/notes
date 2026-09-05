using System.Text.Json.Serialization;
using EzAuth.Interfaces;
using Notes.Interface.DTO;

namespace Notes.Interface;

/// <summary>
/// Source-generated JSON metadata for everything serialized with <see cref="NoteJson.Default"/>
/// (wire payloads, queued changes, auth-backend address). Required so serialization keeps working
/// when reflection-based STJ is disabled (NativeAOT publish).
/// </summary>
[JsonSerializable(typeof(Payload))]
[JsonSerializable(typeof(NoteChange))]
[JsonSerializable(typeof(List<NoteChange>))]
[JsonSerializable(typeof(EzAuthAddress))]
public partial class NoteJsonContext : JsonSerializerContext
{
}
