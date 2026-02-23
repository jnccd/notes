using EzKeycloak;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Notes.Interface
{
    public enum CommsState
    {
        Disconnected,
        Connected,
        Working
    }
    public class Communicator : IDisposable
    {
        readonly object lockject = new object();
        readonly Action<CommsState>? stateChanged;
        public int RequestLoopInterval { get; set; } = 1000;

        readonly CancellationTokenSource serverToken = new();
        public Task? ServerTask { get => serverTask; private set { } }
        Task? serverTask;

        string serverUri;
        KeyCloakHttpClient client;

        public Communicator(string serverUri, string serverUsername, string? initialKeyCloakRefreshToken, Action<string> keyCloakRefreshTokenChanged, Action<CommsState>? stateChanged = null, HttpClient? httpClient = null)
        {
            this.serverUri = serverUri;
            this.stateChanged = stateChanged;
            HttpClientHandler handler = new();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            httpClient ??= new HttpClient(handler);
            client = new(GetKeyCloakAddress(serverUri, httpClient), keyCloakRefreshTokenChanged, initialKeyCloakRefreshToken, httpClient);
        }

        public static KeyCloakAddress GetKeyCloakAddress(string serverUri, HttpClient httpClient)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{serverUri}/keycloak");
            request.Headers.Add("accept", "*/*");
            HttpResponseMessage response = httpClient.SendAsync(request).Result;
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().Result;
            KeyCloakAddress? keyCloakAddress = JsonConvert.DeserializeObject<KeyCloakAddress>(responseBody);
            return keyCloakAddress!;
        }

        public void DoNewLogIn(string username, string password)
        {
            client.LogIn(username, password);
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

        public void SendString(string s)
        {
            Logger.WriteLine($"Sending string...");

            try
            {
                stateChanged?.Invoke(CommsState.Working);
                var httpContent = new StringContent(s, Encoding.UTF8, "application/json");
                using var response = client.PostAsync(serverUri, httpContent).Result;
                stateChanged?.Invoke(response.StatusCode != HttpStatusCode.GatewayTimeout ? CommsState.Connected : CommsState.Disconnected);

                Logger.WriteLine(response.StatusCode);
                //Logger.WriteLine(response.Content.ReadAsStringAsync().Result);
            }
            catch (Exception e)
            {
                Logger.WriteLine(e, LogLevel.Error);
                stateChanged?.Invoke(CommsState.Disconnected);
            }

            Logger.WriteLine($"Sent");
        }
        public Payload? ReqPayload() => ReqPayload(out string _);
        public Payload? ReqPayload(out string receivedText)
        {
            try
            {
                stateChanged?.Invoke(CommsState.Working);
                receivedText = client.GetStringAsync(serverUri).Result;

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
                Logger.WriteLine(e, LogLevel.Error);
                stateChanged?.Invoke(CommsState.Disconnected);
            }

            return null;
        }

        public void Dispose()
        {
            stateChanged?.Invoke(CommsState.Disconnected);
            serverToken.Cancel();
            if (serverTask?.Status != TaskStatus.WaitingForActivation)
                serverTask?.Wait();
            //GC.SuppressFinalize(this);
        }
    }
}
