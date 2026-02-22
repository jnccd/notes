using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EzKeycloak;

public record KeyCloakAddress
{
    public string? KeycloakRealmUrl { get; init; }
    public string? KeycloakClient { get; init; }
}
public class LoginResponse
{
    public string? access_token { get; set; }
    public int? expires_in { get; set; }
    public int? refresh_expires_in { get; set; }
    public string? refresh_token { get; set; }
    public string? token_type { get; set; }
    public string? session_state { get; set; }
    public string? scope { get; set; }
}
public class UserinfoResponse
{
    public string? sub { get; set; }
    public string? name { get; set; }
    public string? preferred_username { get; set; }
    public string? given_name { get; set; }
    public string? family_name { get; set; }
    public string? email { get; set; }
    public bool? email_verified { get; set; }
}
