using EzAuth.Keycloak;

namespace EzAuth.Interfaces;

public class EzAuthException(string message) : Exception(message);

public interface IEzAuth
{
    public abstract static bool IsTokenValid(HttpClient client, string url, string accessToken, out EzAuthUserInfo? userInfo);
    public abstract static EzAuthLoginTokens? Login(HttpClient client, string url, string clientId, string username, string password);
    public abstract static EzAuthLoginTokens? RefreshSession(HttpClient client, string url, string clientId, string refreshToken);
}
