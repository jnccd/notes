using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using NotesServer.Services.Notes;
using static NotesServer.Configuration;

namespace NotesServer.Services.BasicAuth;

[RegisterImplementation(ServiceRegisterType.Singleton, typeof(AuthService))]
public class AuthService(IOptions<AuthOptions> options, LoggerService logger, PersistenceService persistence)
{
    readonly bool writeLogs = options.Value.WriteLogs;
    readonly bool give404 = options.Value.Give404;

    public IResult GetUser(string? authTokenHeader, HttpClient httpClient, Func<User, IResult> handleRequest)
    {
        Result<User> userResult = GetUser(authTokenHeader, httpClient);
        if (userResult.IsSuccess)
            return handleRequest(userResult.Value!);
        else
            return userResult.HttpResult ?? Results.Problem("Unknown error");
    }

    public Result<User> GetUser(string? authTokenHeader, HttpClient httpClient)
    {
        if (authTokenHeader?.Length < 2)
        {
            if (writeLogs)
                logger.WriteLine($"[Auth] Invalid token: {authTokenHeader}");
            return new Result<User>(Results.BadRequest($"Invalid token {authTokenHeader}"));
        }
        if (!EzKeycloak.EzKeycloak.IsTokenValid(httpClient, options.Value.KeycloakRealmUrl ?? "", authTokenHeader?.Split(" ")[1] ?? "", out var userInfo))
        {
            if (writeLogs)
                logger.WriteLine($"[Auth] Invalid token: {authTokenHeader}");
            return new Result<User>(Results.Unauthorized());
        }

        var notesUser = persistence.Users?.FirstOrDefault(u => u.Username == userInfo?.preferred_username);
        if (notesUser == null) persistence.Users?.Append(notesUser = new(userInfo?.preferred_username ?? "unknown"));
        if (notesUser == null) return new Result<User>(give404 ? Results.NotFound() : new AuthReqResult());
        return new Result<User>(notesUser);
    }
}
