using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EzAuth.Keycloak;

public class EzAuthLoginTokens
{
    public string? AccessToken { get; }
    public string? RefreshToken { get; }
}
public class EzAuthUserInfo
{
    /// <summary>
    /// Immutable, Unique.
    /// Can be used as a primary key for database entries.
    /// </summary>
    public string? UserId { get; }
    /// <summary>
    /// A unique name the user selected on registration, not guaranteed to be immutable.
    /// </summary>
    public string? UserHandle { get; }
    /// <summary>
    /// The display name of the user, can be changed by the user at any time, not guaranteed to be unique.
    /// May contain special characters, emojis, etc.
    /// </summary>
    public string? UserDisplayName { get; }
}
