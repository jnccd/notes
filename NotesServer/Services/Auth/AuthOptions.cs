namespace NotesServer.Services.Auth;

public class AuthOptions()
{
    public bool WriteLogs { get; set; }
    public bool Give404 { get; set; }
    public string? KeycloakRealmUrl { get; set; }
    public string? KeycloakClient { get; set; }
}

