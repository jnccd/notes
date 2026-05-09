using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EzAuth.Keycloak;

public record KeyCloakAddress
{
    public string? KeycloakRealmUrl { get; init; }
    public string? KeycloakClient { get; init; }
}
public class LoginResponse : EzAuthLoginTokens
{
    [JsonIgnore]
    public new string? AccessToken { get { return access_token; } }
    [JsonIgnore]
    public new string? RefreshToken { get { return refresh_token; } }

    public string? access_token { get; set; }
    public int? expires_in { get; set; }
    public int? refresh_expires_in { get; set; }
    public string? refresh_token { get; set; }
    public string? token_type { get; set; }
    public string? session_state { get; set; }
    public string? scope { get; set; }
}
public class UserinfoResponse : EzAuthUserInfo
{
    [JsonIgnore]
    public new string? UserId { get { return preferred_username; } }
    [JsonIgnore]
    public new string? UserHandle { get { return preferred_username; } }
    [JsonIgnore]
    public new string? UserDisplayName { get { return given_name; } }

    public string? sub { get; set; }
    public string? name { get; set; }
    public string? preferred_username { get; set; }
    public string? given_name { get; set; }
    public string? family_name { get; set; }
    public string? email { get; set; }
    public bool? email_verified { get; set; }
}
