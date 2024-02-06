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
    public class Communicator : IDisposable
    {
        readonly public string serverUri;
        readonly string serverUsername;
        readonly string serverPassword;
        readonly Func<Payload> RequestedPayloadUpdate;
        readonly Logger logger;

        readonly CancellationTokenSource serverToken = new CancellationTokenSource();
        public Task? ServerTask { get => serverTask; private set { } }
        Task? serverTask;
        HttpClient client;
        readonly object lockject;

        public Communicator(string serverUri, string serverUsername, string serverPassword, Func<Payload> RequestedPayloadUpdate, Logger logger)
        {
            lockject = new object();

            this.serverUri = serverUri;
            this.serverUsername = serverUsername;
            this.serverPassword = serverPassword;
            this.RequestedPayloadUpdate = RequestedPayloadUpdate;
            this.logger = logger;

            HttpClientHandler handler = new HttpClientHandler();
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

                            logger.WriteLine($"Recived {receivedText} from {serverUri}", false);

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
            logger.WriteLine($"Sending {s}");

            try
            {
                using var response = client.PostAsync(serverUri, new StringContent(s, Encoding.UTF8, "application/json")).Result;

                logger.WriteLine(response.StatusCode);
                logger.WriteLine(response.Content.ReadAsStringAsync().Result);
            }
            catch (Exception e)
            {
                logger.WriteLine(e);
            }

            logger.WriteLine($"Sent");
        }
        public Payload? ReqPayload() => ReqPayload(out string _);
        public Payload? ReqPayload(out string receivedText)
        {
            try
            {
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
                        catch (Exception e) { logger.WriteLine($"Error parsing payload: {e}"); }
                        return receivedPayload;
                    }
                }
            }
            catch (Exception e)
            {
                receivedText = "";
                logger.WriteLine(e);
            }
            
            return null;
        }

        public void Dispose()
        {
            serverToken.Cancel();
            if (serverTask?.Status != TaskStatus.WaitingForActivation)
                serverTask?.Wait();
            //GC.SuppressFinalize(this);
        }
    }
}
