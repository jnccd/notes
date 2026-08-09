using EzAuth.Interfaces;
using EzAuth.Keycloak;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Notes.Interface.DTO;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace Notes.Interface;

public enum CommsState
{
    Disconnected,
    Connected,
    Working
}
public class Communicator : IDisposable
{
    const string ROUTE_VERSION_PREFIX = "/v1";

    readonly object lockject = new object();
    readonly Action<CommsState>? stateChanged;
    readonly Action<Exception>? onPayloadRequestError;
    public int RequestLoopInterval { get; set; } = 1000;

    readonly CancellationTokenSource serverToken = new();
    public Task? ServerTask { get => serverTask; private set { } }
    Task? serverTask;

    string serverUri;
    IEzAuthHttpClient client;
    IEzAuth auth;
    EzAuthAddress authBackendAddress;
    HttpClient? _httpClient;

    public Communicator(string serverUri, string? initialAuthBackendRefreshToken, Action<string> authBackendRefreshTokenChanged, Action<CommsState>? stateChanged = null, HttpClient? httpClient = null, Action<Exception>? onPayloadRequestError = null)
    {
        this.serverUri = serverUri;
        this.stateChanged = stateChanged;
        this.onPayloadRequestError = onPayloadRequestError;
        _httpClient = httpClient ?? new HttpClient();

        auth = new EzKeycloak();
        authBackendAddress = GetAuthBackendAddress(serverUri, _httpClient);
        client = new KeyCloakHttpClient(authBackendAddress, authBackendRefreshTokenChanged, initialAuthBackendRefreshToken, httpClient);
    }

    public static EzAuthAddress GetAuthBackendAddress(string serverUri, HttpClient httpClient)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{serverUri}{ROUTE_VERSION_PREFIX}/authBackend");
        request.Headers.Add("accept", "*/*");
        HttpResponseMessage response = httpClient.SendAsync(request).Result;
        response.EnsureSuccessStatusCode();
        string responseBody = response.Content.ReadAsStringAsync().Result;
        EzAuthAddress? authBackendAddress = JsonConvert.DeserializeObject<EzAuthAddress>(responseBody);
        return authBackendAddress!;
    }

    public string GetSeparateSessionRefreshToken(string username, string password)
    {
        var res = auth.Login(_httpClient!, authBackendAddress.RealmUrl!, authBackendAddress.Client!, username, password);
        return res!.RefreshToken!;
    }

    public void DoNewLogIn(string username, string password)
    {
        client.Login(username, password);
    }

    public void StartRequestLoop(Action<string, Payload?> receivedEvent)
    {
        serverTask = Task.Run(() =>
        {
            Thread.CurrentThread.Name = "Server Thread";

            string last = "";

            while (true)
            {
                if (serverToken.IsCancellationRequested)
                    break;

                try
                {
                    try
                    {
                        var receivedPayload = ReqPayload(out string receivedText);

                        if (receivedText == last)
                        {
                            Task.Delay(RequestLoopInterval).Wait();
                            continue;
                        }

                        Logger.WriteLine($"Received payload from {serverUri}");

                        receivedEvent(receivedText, receivedPayload);

                        last = receivedText;
                        Task.Delay(RequestLoopInterval).Wait();
                    }
                    catch (OperationCanceledException) { break; }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    Task.Delay(RequestLoopInterval).Wait();
                }
            }
            serverToken.Dispose();
        }, serverToken.Token);
    }

    /// <summary>
    /// Sends a batch of note changes to the server. Returns a list of unfinished note changes if any errors occurred.
    /// If the list is empty, all changes were successfully sent.
    /// </summary>
    /// <param name="noteChanges">List of note changes to send, after their sending operation elements are removed in place from this list</param>
    public void SendChanges(List<NoteChange> noteChanges)
    {
        Logger.WriteLine($"Sending change...");

        try
        {
            var s = JsonConvert.SerializeObject(noteChanges, Formatting.Indented);

            stateChanged?.Invoke(CommsState.Working);
            var httpContent = new StringContent(s, Encoding.UTF8, "application/json");
            using var response = client.PostAsync($"{serverUri}{ROUTE_VERSION_PREFIX}/notes/batch", httpContent).Result;
            stateChanged?.Invoke(response.StatusCode != HttpStatusCode.GatewayTimeout ? CommsState.Connected : CommsState.Disconnected);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = response.Content.ReadAsStringAsync().Result;
                onPayloadRequestError?.Invoke(new Exception($"{response.StatusCode}: {errorContent}"));
                Logger.WriteLine($"Error sending changes: {response.StatusCode}: {errorContent}");
            }

            Logger.WriteLine(response.StatusCode);
            //Logger.WriteLine(response.Content.ReadAsStringAsync().Result);
        }
        catch (Exception e)
        {
            Logger.WriteLine(e, LogLevel.Error);
            stateChanged?.Invoke(CommsState.Disconnected);
        }

        Logger.WriteLine($"Sent");
        noteChanges.Clear();
    }
    public Payload? ReqPayload() => ReqPayload(out string _);
    public Payload? ReqPayload(out string receivedText)
    {
        try
        {
            stateChanged?.Invoke(CommsState.Working);
            receivedText = client.GetStringAsync($"{serverUri}{ROUTE_VERSION_PREFIX}/notes").Result;

            StringBuilder sb = new StringBuilder(receivedText);
            sb.Replace("\\\n", "");
            sb.Replace("\\n", "");
            sb.Replace("\\\"", "\"");
            sb.Replace("\\\"", "\"");
            sb.Replace("\r", "");
            sb.Replace("\n", "");
            //sb.Replace("\\", "");
            receivedText = sb.ToString();
            receivedText = receivedText.Trim('"');

            //logger.WriteLine($"Recived {receivedText} from {serverUri}");

            if (!string.IsNullOrWhiteSpace(receivedText))
            {
                lock (lockject)
                {
                    Payload? receivedPayload = null;
                    try { receivedPayload = Payload.Parse(receivedText); }
                    catch (Exception e) { Logger.WriteLine($"Error parsing payload: {e}"); }
                    stateChanged?.Invoke(CommsState.Connected);
                    return receivedPayload;
                }
            }
        }
        catch (Exception e)
        {
            receivedText = "";

            if (onPayloadRequestError != null)
                onPayloadRequestError(e);
            else
                Logger.WriteLine(e, LogLevel.Error);
            stateChanged?.Invoke(CommsState.Disconnected);
        }

        return null;
    }

    public void Dispose()
    {
        stateChanged?.Invoke(CommsState.Disconnected);
        try { serverToken.Cancel(); } catch { }
        if (serverTask?.Status != TaskStatus.WaitingForActivation)
            serverTask?.Wait();
        //GC.SuppressFinalize(this);
    }
}
