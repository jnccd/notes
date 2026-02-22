using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using NotesServer.Services.Notes;
using static NotesServer.Configuration;

namespace NotesServer.Services.BasicAuth;

[RegisterImplementation(ServiceRegisterType.Singleton, typeof(AuthService))]
public class AuthService(IOptions<AuthOptions> options, PersistenceService persistence)
{
    readonly bool writeLogs = options.Value.WriteLogs;
    readonly bool give404 = options.Value.Give404;

    public IResult GetUser(string? authTokenHeader, HttpClient httpClient, Func<User?, IResult> handleRequest)
    {
        User? u;
        if ((u = GetUser(authTokenHeader, httpClient)) != null)
            return handleRequest(u);
        else
            return give404 ? Results.NotFound() : new AuthReqResult();
    }

    public User? GetUser(string? authTokenHeader, HttpClient httpClient)
    {
        Console.WriteLine($"[Auth] Attempting to authenticate with token: {authTokenHeader?.Split(" ")[1]} on {options.Value.KeycloakRealmUrl}");
        if (!EzKeycloak.EzKeycloak.IsTokenValid(httpClient, options.Value.KeycloakRealmUrl ?? "", authTokenHeader?.Split(" ")[1] ?? "", out var userInfo))
        {
            if (writeLogs)
                Debug.WriteLine($"[Auth] Invalid token: {authTokenHeader}");
            return null;
        }

        var notesUser = persistence.Users?.FirstOrDefault(u => u.Username == userInfo?.preferred_username);
        if (notesUser == null) persistence.Users?.Append(notesUser = new(userInfo?.preferred_username ?? "unknown"));
        return notesUser;
    }
}
