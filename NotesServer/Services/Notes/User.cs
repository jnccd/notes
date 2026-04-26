using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Notes.Interface;

namespace NotesServer.Services.Notes;

public class User
{
    static JsonSerializerOptions jsonOptions = new JsonSerializerOptions
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public Guid Id { get; set; }
    public string Username { get; private set; }

    // Ef Core doesnt support recursive jsonb, so I serialize manually
    [NotMapped]
    public Payload? NotesPayload { get; set; }
    public string? NotesPayloadJson
    {
        get => JsonSerializer.Serialize(NotesPayload, jsonOptions);
        set
        {
            if (value != null)
                NotesPayload = JsonSerializer.Deserialize<Payload>(value, jsonOptions);
        }
    }

    private User() { Username = ""; }
    public User(string username)
    {
        Id = Guid.NewGuid();
        Username = username;
        NotesPayload = new();
    }
}
