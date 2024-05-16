using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        readonly public string serverUri;
        public readonly string serverUsername;
        readonly Action<CommsState>? stateChanged;

        readonly CancellationTokenSource serverToken = new();
        public Task? ServerTask { get => serverTask; private set { } }
        Task? serverTask;
        HttpClient client;
        readonly object lockject;

        public Communicator(string serverUri, string serverUsername, string serverPassword, Action<CommsState>? stateChanged = null)
        {
            lockject = new object();

            this.serverUri = serverUri;
            this.serverUsername = serverUsername;
            this.stateChanged = stateChanged;

            HttpClientHandler handler = new();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            client = new HttpClient(handler);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic",
                    $"{Convert.ToBase64String(Encoding.UTF8.GetBytes($"{serverUsername}:{serverPassword}"))}");
        }

        public void StartRequestLoop(Action<string, Payload?> receivedEvent)
        {
            serverTask = Task.Run(() => {
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
                                Task.Delay(1000).Wait();
                                continue;
                            }

                            Logger.WriteLine($"Recived {receivedText} from {serverUri}");

                            receivedEvent(receivedText, receivedPayload);

                            last = receivedText;
                            Task.Delay(1000).Wait();
                        }
                        catch (OperationCanceledException) { break; }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.ToString());
                        Task.Delay(1000).Wait();
                    }
                }
                serverToken.Dispose();
            }, serverToken.Token);
        }

        public void SendString(string s)
        {
            Logger.WriteLine($"Sending {s}");

            try
            {
                stateChanged?.Invoke(CommsState.Working);
                using var response = client.PostAsync(serverUri, new StringContent(s, Encoding.UTF8, "application/json")).Result;
                stateChanged?.Invoke(response.StatusCode == HttpStatusCode.OK ? CommsState.Connected : CommsState.Disconnected);

                Logger.WriteLine(response.StatusCode);
                Logger.WriteLine(response.Content.ReadAsStringAsync().Result);
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
