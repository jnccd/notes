using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Notes.Interface;
using NotesServer.BasicAuth;
using NotesServer.Notes;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace NotesServer
{
    public static class Configuration
    {
        public static void ConfigureWebhost(this WebApplicationBuilder builder)
        {
            ushort port = string.IsNullOrWhiteSpace(builder.Configuration["PORT"]) ?
                (ushort)7777 :
                Convert.ToUInt16(builder.Configuration["PORT"]);

            X509Certificate2 x509 = GetCertificateFromConfig(builder);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Any, port, listenOptions =>
                {
                    listenOptions.UseHttps(x509);
                });
            });
        }

        private static X509Certificate2 GetCertificateFromConfig(WebApplicationBuilder builder)
        {
            char _s = Path.DirectorySeparatorChar;
            if (string.IsNullOrWhiteSpace(builder.Configuration["CERT_PATH"]))
            {
                Logger.WriteLine($"CERT_PATH is empty!");
                throw new ArgumentException("CERT_PATH is empty!");
            }
            var certPem = File.ReadAllText($"{builder.Configuration["CERT_PATH"]}{_s}fullchain.pem");
            var keyPem = File.ReadAllText($"{builder.Configuration["CERT_PATH"]}{_s}privkey.pem");
            var x509 = X509Certificate2.CreateFromPem(certPem, keyPem);
            return x509;
        }

        public static void RegisterServices(this WebApplicationBuilder builder)
        {
            builder.Configuration.GetSection(nameof(BasicAuthOptions)).Bind(new BasicAuthOptions());

            builder.Services
                .AddSingleton<IBasicAuthService, BasicAuthService>()
                .AddSingleton<INotesEnvironmentService, NotesEnvironmentService>();
        }

        public static void RegisterMiddlewares(this WebApplication app)
        {
            app.UseHttpsRedirection();
        }
    }
}
