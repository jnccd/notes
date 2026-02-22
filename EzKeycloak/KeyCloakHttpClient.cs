using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EzKeycloak;

public class KeyCloakHttpClient(KeyCloakAddress keyCloakAddress, Action<string> keyCloakRefreshTokenChanged, string? initialRefreshToken = null, HttpClient? client = null)
{
    public HttpClient client = client ?? new HttpClient();
    string? currentAccessToken = null;
    DateTime accessTokenExpiry = DateTime.MinValue;
    string? currentRefreshToken = initialRefreshToken;
    DateTime refreshTokenExpiry = DateTime.MinValue;
    bool errorDuringTokenRetrieval = false;
    const int tokenRefreshBufferSeconds = 10;

    public void LogIn(string username, string password)
    {
        var res = EzKeycloak.LoginToCloak(client, keyCloakAddress!.KeycloakRealmUrl!, keyCloakAddress.KeycloakClient!, username, password);
        errorDuringTokenRetrieval = UpdateTokenVars(res);

        if (currentRefreshToken != null)
            keyCloakRefreshTokenChanged(currentRefreshToken);
    }

    public bool NewLogInNeeded()
    {
        if (currentRefreshToken == null || errorDuringTokenRetrieval || DateTime.Now >= refreshTokenExpiry)
            return true;
        return false;
    }

    public void RefreshTokenIfNeeded()
    {
        if (currentRefreshToken == null)
            return;

        if (DateTime.Now >= accessTokenExpiry)
        {
            var res = EzKeycloak.RefreshCloakSession(client, keyCloakAddress!.KeycloakRealmUrl!, keyCloakAddress.KeycloakClient!, currentRefreshToken);
            errorDuringTokenRetrieval = UpdateTokenVars(res);
            if (!errorDuringTokenRetrieval && currentRefreshToken != null)
                keyCloakRefreshTokenChanged(currentRefreshToken);
        }
    }

    bool UpdateTokenVars(LoginResponse? res)
    {
        if (res?.access_token == null || res.refresh_token == null)
            return true;
        if (res?.expires_in == null || res.refresh_expires_in == null)
            return true;
        currentAccessToken = res.access_token;
        accessTokenExpiry = DateTime.Now.AddSeconds((double)res.expires_in - tokenRefreshBufferSeconds);
        currentRefreshToken = res.refresh_token;
        refreshTokenExpiry = DateTime.Now.AddSeconds((double)res.refresh_expires_in - tokenRefreshBufferSeconds);
        return false;
    }

    public Task<HttpResponseMessage> PostAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, HttpContent? content)
    {
        RefreshTokenIfNeeded();
        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.Add("Authorization", $"Basic {currentAccessToken}");
        return client.PostAsync(requestUri, content);
    }

    public Task<string> GetStringAsync([StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri)
    {
        RefreshTokenIfNeeded();
        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.Add("Authorization", $"Basic {currentAccessToken}");
        return client.GetStringAsync(requestUri);
    }
}