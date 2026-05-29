namespace NotesServer.Services.Auth;

public class AuthOptions()
{
    public bool WriteLogs { get; set; }
    public bool Give404 { get; set; }
    public string? AuthBackendRealmUrl { get; set; }
    public string? AuthBackendClient { get; set; }
}

