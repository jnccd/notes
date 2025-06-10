using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Notes.Interface;

namespace NotesServer.Services.Notes;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; private set; }
    public string PasswordHash { get; private set; }

    // Ef Core doesnt support recursive jsonb, so I serialize manually
    [NotMapped]
    public Payload? NotesPayload { get; set; }
    public string? NotesPayloadJson
    {
        get => JsonSerializer.Serialize(NotesPayload);
        set
        {
            if (value != null)
                NotesPayload = JsonSerializer.Deserialize<Payload>(value);
        }
    }

    private User() { Username = ""; PasswordHash = ""; }
    public User(string username, string password)
    {
        Id = Guid.NewGuid();
        Username = username;
        PasswordHash = password.GetStringHash();
        NotesPayload = new();
    }
}
