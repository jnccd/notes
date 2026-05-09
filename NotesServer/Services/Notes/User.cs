using System.ComponentModel.DataAnnotations;
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

    /// <summary>
    /// Immutable, Unique.
    /// Can be used as a primary key.
    /// </summary>
    [Key]
    public string UserId { get; }
    /// <summary>
    /// A unique name the user selected on registration, not guaranteed to be immutable.
    /// </summary>
    public string UserHandle { get; }
    /// <summary>
    /// The display name of the user, can be changed by the user at any time, not guaranteed to be unique.
    /// May contain special characters, emojis, etc.
    /// </summary>
    public string UserDisplayName { get; }

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

    private User() { UserId = ""; UserHandle = ""; UserDisplayName = ""; }
    public User(string UserId, string UserHandle, string UserDisplayName)
    {
        this.UserId = UserId;
        this.UserHandle = UserHandle;
        this.UserDisplayName = UserDisplayName;
        NotesPayload = new();
    }
}
