using EzAuth.Interfaces;
using EzAuth.Keycloak;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

    readonly string serverUri;
    readonly string? initialAuthBackendRefreshToken;
    readonly Action<string> authBackendRefreshTokenChanged;
    readonly IEzAuth auth;
    readonly HttpClient httpClient;

    readonly object initLock = new();
    EzAuthAddress? authBackendAddress;
    IEzAuthHttpClient? client;
    string? lastReportedError = null;

    /// <summary>Last state reported to the UI; used to avoid hammering a dead server.</summary>
    public CommsState State { get; private set; } = CommsState.Disconnected;
    string? lastReportedSendError = null;
    DateTime lastSendFailureAt = DateTime.MinValue;

    void ReportState(CommsState newState)
    {
        State = newState;
        try { stateChanged?.Invoke(newState); }
        catch (Exception ex) { Logger.WriteLine($"Error on stateChanged: {ex}"); }
    }

    public Communicator(string serverUri, string? initialAuthBackendRefreshToken, Action<string> authBackendRefreshTokenChanged, Action<CommsState>? stateChanged = null, HttpClient? httpClient = null, Action<Exception>? onPayloadRequestError = null)
    {
        this.serverUri = serverUri;
        this.stateChanged = stateChanged;
        this.onPayloadRequestError = onPayloadRequestError;
        this.initialAuthBackendRefreshToken = initialAuthBackendRefreshToken;
        this.authBackendRefreshTokenChanged = authBackendRefreshTokenChanged;
        this.httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        auth = new EzKeycloak();
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

    void EnsureInitialized()
    {
        if (client != null)
            return;
        lock (initLock)
        {
            if (client != null)
                return;
            authBackendAddress = GetAuthBackendAddress(serverUri, httpClient);
            client = new KeyCloakHttpClient(authBackendAddress, authBackendRefreshTokenChanged, initialAuthBackendRefreshToken, httpClient);
        }
    }

    public string GetSeparateSessionRefreshToken(string username, string password)
    {
        EnsureInitialized();
        var res = auth.Login(httpClient, authBackendAddress!.RealmUrl!, authBackendAddress!.Client!, username, password);
        return res!.RefreshToken!;
    }

    public void DoNewLogIn(string username, string password)
    {
        EnsureInitialized();
        client!.Login(username, password);
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
    /// Sends a batch of note changes to the server. Only changes the server has
    /// definitively processed are removed in place from <paramref name="noteChanges"/>:
    /// changes that were applied (per-change HTTP 2xx) and changes rejected as
    /// conflicts that can never succeed by re-sending them (per-change HTTP 4xx,
    /// e.g. a note id that already exists on the server, a missing parent note or an
    /// out of bounds insertion index). Changes that could not be delivered (offline,
    /// timeouts, server errors, auth failures) are left in the list so the caller can
    /// retry them later — once the connection is back they are rolled out to the server
    /// on the next call.
    /// </summary>
    /// <param name="noteChanges">List of note changes to send. Settled elements are removed in place; the rest stay queued for a retry.</param>
    public void SendChanges(List<NoteChange> noteChanges)
    {
        if (noteChanges == null || noteChanges.Count == 0)
            return;

        // While we believe the server is unreachable, wait for the request loop to
        // confirm the connection is back instead of hammering the network on every
        // autosave tick.
        if (State == CommsState.Disconnected)
            return;

        // Back off after a failed batch: don't re-attempt faster than one
        // request-loop interval, otherwise a sick server would be hammered at the
        // autosave loop's cadence.
        if (lastSendFailureAt != DateTime.MinValue && DateTime.Now - lastSendFailureAt < TimeSpan.FromMilliseconds(RequestLoopInterval))
            return;

        Logger.WriteLine($"Sending change...");

        // Work on a snapshot so UI changes made while the request runs are neither
        // sent by this call nor removed by it afterwards.
        List<NoteChange> toSend;
        try { toSend = new List<NoteChange>(noteChanges); }
        catch (Exception e) { Logger.WriteLine($"Could not snapshot unsynced changes: {e}", LogLevel.Error); return; }

        try
        {
            EnsureInitialized();
            var s = JsonConvert.SerializeObject(toSend, Formatting.Indented);

            ReportState(CommsState.Working);
            var httpContent = new StringContent(s, Encoding.UTF8, "application/json");
            using var response = client!.PostAsync($"{serverUri}{ROUTE_VERSION_PREFIX}/notes/batch", httpContent).Result;
            ReportState(response.StatusCode != HttpStatusCode.GatewayTimeout ? CommsState.Connected : CommsState.Disconnected);

            Logger.WriteLine(response.StatusCode);
            string content = response.Content.ReadAsStringAsync().Result;

            // The server answers with one HttpResult per sent change:
            //   2xx -> the change was applied (or was already a no-op on the server),
            //   4xx -> the change is a conflict and will never succeed by re-sending,
            // so neither must be sent again. 5xx/unknown -> keep it queued for retry.
            // The server emits a verdict for every change of a batch (and no per-change
            // 5xx today), so a settled prefix/pattern can be removed without breaking
            // the insertion-index bookkeeping of the changes that stay queued.
            int[]? perChangeStatuses = TryParseBatchStatuses(content, toSend.Count);
            if (perChangeStatuses == null)
            {
                // No usable per-change results (timeout, server error, auth failure,
                // non-JSON body...): keep the whole batch queued for a later retry.
                lastSendFailureAt = DateTime.Now;
                string errorText = $"{response.StatusCode}: {content}";
                if (!response.IsSuccessStatusCode && errorText != lastReportedSendError)
                {
                    lastReportedSendError = errorText;
                    try { onPayloadRequestError?.Invoke(new Exception(errorText)); }
                    catch (Exception e) { Logger.WriteLine($"Error on onPayloadRequestError: {e}"); }
                }
                Logger.WriteLine($"Batch not confirmed; keeping all {toSend.Count} change(s) queued for retry ({response.StatusCode})");
                return;
            }

            lastReportedSendError = null;          // report the next distinct error again
            int removed = 0;
            for (int i = 0; i < toSend.Count; i++)
            {
                int status = perChangeStatuses[i];
                if (status >= 200 && status < 500)
                {
                    try { noteChanges.Remove(toSend[i]); }
                    catch (Exception e) { Logger.WriteLine($"Could not remove handled change from queue: {e}", LogLevel.Error); }
                    removed++;
                    if (status >= 400)
                        Logger.WriteLine($"Change {i} ({toSend[i].Type} of note {toSend[i].NoteId}) rejected by server (HTTP {status}); dropped from queue so it is not re-sent as a conflict");
                }
                else
                {
                    Logger.WriteLine($"Change {i} ({toSend[i].Type} of note {toSend[i].NoteId}) has unknown status {status}; keeping it queued for retry");
                }
            }
            if (removed < toSend.Count)
                lastSendFailureAt = DateTime.Now; // something stayed queued; back off before retrying it
            else
                lastSendFailureAt = DateTime.MinValue; // the whole batch was settled; connection is usable
            Logger.WriteLine($"SendChanges: {removed}/{toSend.Count} settled and removed from queue, {noteChanges.Count} still queued");
        }
        catch (Exception e)
        {
            // Network failure / timeout / offline: keep everything queued so it can be
            // rolled out once the connection is back.
            Logger.WriteLine(e, LogLevel.Error);
            lastSendFailureAt = DateTime.Now;
            ReportState(CommsState.Disconnected);
        }
    }

    /// <summary>
    /// Parses the per-change HTTP status codes from a /notes/batch response body
    /// (a NotesBatchPostResult JSON: {"results":[{"statusCode":200,"content":null},...]}).
    /// Returns null when the body does not contain one result per sent change.
    /// </summary>
    static int[]? TryParseBatchStatuses(string content, int expectedCount)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;
        try
        {
            var obj = JObject.Parse(content);
            var arr = obj["results"] as JArray ?? obj["Results"] as JArray;
            if (arr == null || arr.Count != expectedCount)
                return null;
            int[] statuses = new int[expectedCount];
            for (int i = 0; i < expectedCount; i++)
            {
                var item = arr[i] as JObject;
                var statusToken = item?["statusCode"] ?? item?["StatusCode"];
                if (statusToken == null)
                    return null;
                statuses[i] = statusToken.Value<int>();
            }
            return statuses;
        }
        catch (Exception e)
        {
            Logger.WriteLine($"Could not parse batch response: {e}", LogLevel.Error);
            return null;
        }
    }
    public Payload? ReqPayload() => ReqPayload(out string _);
    public Payload? ReqPayload(out string receivedText)
    {
        try
        {
            EnsureInitialized();
            ReportState(CommsState.Working);
            receivedText = client!.GetStringAsync($"{serverUri}{ROUTE_VERSION_PREFIX}/notes").Result;

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
                    lastReportedError = null; // connection recovered; report the next distinct error again
                    Payload? receivedPayload = null;
                    try { receivedPayload = Payload.Parse(receivedText); }
                    catch (Exception e) { Logger.WriteLine($"Error parsing payload: {e}"); }
                    ReportState(CommsState.Connected);
                    return receivedPayload;
                }
            }
        }
        catch (Exception e)
        {
            receivedText = "";

            // Only report a given error once (the request loop retries every few seconds,
            // so without this the "Error Connecting to Server!" popup would spam the UI).
            if (e.ToString() != lastReportedError)
            {
                lastReportedError = e.ToString();
                if (onPayloadRequestError != null)
                    onPayloadRequestError(e);
                else
                    Logger.WriteLine(e, LogLevel.Error);
            }
            ReportState(CommsState.Disconnected);
        }

        return null;
    }

    public void Dispose()
    {
        ReportState(CommsState.Disconnected);
        try { serverToken.Cancel(); } catch { }
        if (serverTask?.Status != TaskStatus.WaitingForActivation)
            serverTask?.Wait();
        //GC.SuppressFinalize(this);
    }
}
