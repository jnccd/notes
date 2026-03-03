using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EzKeycloak;

public class EzKeycloakException(string message) : Exception(message);

public static class EzKeycloak
{
    public static LoginResponse? LoginToCloakReq(HttpClient client, HttpRequestMessage request, string Content, (string, string)[]? AdditionalHeaders = null)
    {
        if (AdditionalHeaders != null)
        {
            foreach (var header in AdditionalHeaders)
            {
                request.Headers.Add(header.Item1, header.Item2);
            }
        }
        request.Content = new StringContent(Content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        HttpResponseMessage response = client.SendAsync(request).Result;
        if (!response.IsSuccessStatusCode)
            throw new EzKeycloakException($"LoginToCloakReq to {request.RequestUri} failed! {response.StatusCode} {response.Content.ReadAsStringAsync().Result}");
        string responseBody = response.Content.ReadAsStringAsync().Result;
        LoginResponse? loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseBody);
        return loginResponse;
    }
    public static UserinfoResponse? UserinfoCloakReq(HttpClient client, HttpRequestMessage request, string? AuthorizationBearer = null)
    {
        if (AuthorizationBearer != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthorizationBearer);
        HttpResponseMessage response = client.Send(request);
        if (!response.IsSuccessStatusCode)
            throw new EzKeycloakException($"UserinfoCloakReq to {request.RequestUri} failed! {response.StatusCode} {response.Content.ReadAsStringAsync().Result}");
        string responseBody = response.Content.ReadAsStringAsync().Result;
        UserinfoResponse? userinfoResponse = JsonSerializer.Deserialize<UserinfoResponse>(responseBody);
        return userinfoResponse;
    }

    public static bool IsTokenValid(HttpClient client, string realmUrl, string accessToken, out UserinfoResponse? userInfo)
    {
        userInfo = UserinfoCloakReq(client, new HttpRequestMessage(HttpMethod.Get, $"{realmUrl}/protocol/openid-connect/userinfo"), accessToken);
        return userInfo != null;
    }
    public static LoginResponse? LoginToCloak(HttpClient client, string realmUrl, string clientId, string username, string password)
    {
        Debug.WriteLine($"Attempting to login to Keycloak realm {realmUrl} for client {clientId} with user {username} and password {password}");
        var content = $"client_id={clientId}&grant_type=password&username={WebUtility.UrlEncode(username)}&password={WebUtility.UrlEncode(password)}&scope=openid";
        var res = LoginToCloakReq(client, new HttpRequestMessage(HttpMethod.Post, $"{realmUrl}/protocol/openid-connect/token"), content) ?? throw new EzKeycloakException("Login failed: No response");
        return res;
    }
    public static LoginResponse? RefreshCloakSession(HttpClient client, string realmUrl, string clientId, string refreshToken)
    {
        Debug.WriteLine($"Attempting to refresh Keycloak session for client {clientId} and refresh token {refreshToken}");
        var content = $"grant_type=refresh_token&client_id={clientId}&refresh_token={refreshToken}";
        var res = LoginToCloakReq(client, new HttpRequestMessage(HttpMethod.Post, $"{realmUrl}/protocol/openid-connect/token"), content) ?? throw new EzKeycloakException("RefreshCloakSession failed: No response");
        return res;
    }
}
