using System.Diagnostics;
using System.Text;
using EzAuth.Interfaces;
using Microsoft.Extensions.Options;
using NotesServer.Services.Notes;
using static NotesServer.Configuration;

namespace NotesServer.Services.Auth;

[RegisterImplementation(ServiceRegisterType.Singleton, typeof(AuthService))]
public class AuthService(IOptions<AuthOptions> options, LoggerService logger, IEzAuth authBackendService)
{
    readonly bool writeLogs = options.Value.WriteLogs;
    readonly bool give404 = options.Value.Give404;

    public IResult GetUser(string? authTokenHeader, HttpClient httpClient, NotesDbContext notesDbContext, Func<User, IResult> handleRequest)
    {
        Result<User> userResult = GetUser(authTokenHeader, httpClient, notesDbContext);
        if (userResult.IsSuccess)
            return handleRequest(userResult.Value!);
        else
            return userResult.HttpResult ?? Results.Problem("Unknown error");
    }

    public Result<User> GetUser(string? authTokenHeader, HttpClient httpClient, NotesDbContext notesDbContext)
    {
        if (authTokenHeader?.Length < 2)
        {
            if (writeLogs)
                logger.WriteLine($"[Auth] Invalid token: {authTokenHeader}");
            return new Result<User>(Results.BadRequest($"Invalid token {authTokenHeader}"));
        }
        EzAuthUserInfo? userInfo;
        try
        {
            if (!authBackendService.IsTokenValid(httpClient, options.Value.AuthBackendRealmUrl ?? "", authTokenHeader?.Split(" ")[1] ?? "", out userInfo))
            {
                if (writeLogs)
                    logger.WriteLine($"[Auth] Invalid token: {authTokenHeader}");
                return new Result<User>(Results.Unauthorized());
            }
        }
        catch (Exception ex)
        {
            if (writeLogs)
                logger.WriteLine($"[Auth] Token check for {authTokenHeader} failed: {ex}");
            return new Result<User>(Results.BadRequest($"Token check failed: {ex.Message}"));
        }

        var notesUser = notesDbContext.Users?.FirstOrDefault(u => userInfo != null && u.UserId == userInfo.UserId);
        if (notesUser == null && userInfo?.UserId != null)
        {
            notesDbContext.Users?.Add(notesUser = new(
                    userInfo?.UserId ?? throw new ArgumentNullException(nameof(userInfo.UserId)),
                    userInfo?.UserHandle ?? throw new ArgumentNullException(nameof(userInfo.UserHandle)),
                    userInfo?.UserDisplayName ?? throw new ArgumentNullException(nameof(userInfo.UserDisplayName))));
            notesDbContext.SaveChanges();
        }
        if (notesUser == null) return new Result<User>(give404 ? Results.NotFound() : new AuthReqResult());
        return new Result<User>(notesUser);
    }
}
