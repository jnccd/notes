using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Notes.Interface;
using NotesServer.Services;
using NotesServer.Services.BasicAuth;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace NotesServer;

public static class Configuration
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
    public class RegisterImplementation(ServiceRegisterType serviceRegisterType, Type serviceType) : Attribute
    {
        public readonly ServiceRegisterType serviceRegisterType = serviceRegisterType;
        public readonly Type serviceType = serviceType;
    }

    public enum ServiceRegisterType { Singleton, Transient }

    public static void RegisterServices(this WebApplicationBuilder builder)
    {
        // Local Assembly Services
        Type[] serviceTypes = (from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
                               from declaringType in domainAssembly.GetTypes()
                               where declaringType.Module == typeof(Configuration).Module
                                   && declaringType.CustomAttributes.Any(x => x.AttributeType == typeof(RegisterImplementation))
                               select declaringType).ToArray();
        foreach (var declaringType in serviceTypes)
        {
            var attr = declaringType.GetCustomAttribute<RegisterImplementation>();

            if (attr == null || attr?.serviceType == null || attr?.serviceRegisterType == null) continue;

            if (attr.serviceRegisterType == ServiceRegisterType.Singleton)
                builder.Services.AddSingleton(declaringType, attr.serviceType);
            else if (attr?.serviceRegisterType == ServiceRegisterType.Transient)
                builder.Services.AddTransient(declaringType, attr.serviceType);
        }

        // Swagger
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c => c.UseOneOfForPolymorphism());

        // Options
        builder.Configuration.GetSection(nameof(BasicAuthOptions)).Bind(new BasicAuthOptions());
    }

    public static void ConfigureWebhost(this WebApplicationBuilder builder)
    {
        ushort port = string.IsNullOrWhiteSpace(builder.Configuration["PORT"]) ?
            (ushort)7777 :
            Convert.ToUInt16(builder.Configuration["PORT"]);

        if (!string.IsNullOrWhiteSpace(builder.Configuration["CERT_PATH"]))
        {
            X509Certificate2 x509 = GetCertificateFromConfig(builder);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Any, port, listenOptions =>
                {
                    listenOptions.UseHttps(x509);
                });
            });
        }
        else
        {
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Any, port, listenOptions => { });
            });
        }

        builder.Services.AddCors();
    }

    public static void ConfigureWebApp(this WebApplication app)
    {
        var logger = app.Services.GetService(typeof(LoggerService)) as LoggerService;
#if DEBUG
        logger!.WriteLine("Launching in development mode!");
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseCors(policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
#endif
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

    public static void RegisterMiddlewares(this WebApplication app)
    {
        if (!string.IsNullOrWhiteSpace(app.Configuration["CERT_PATH"]))
            app.UseHttpsRedirection();

        app.AddRequestLoggingMiddleware();
    }

    public static void AddRequestLoggingMiddleware(this WebApplication app)
    {
        var logger = app.Services.GetService(typeof(LoggerService)) as LoggerService;
        app.Use(async (context, next) =>
        {
            if (context.Request.Method != "GET")
                try
                {
                    logger?.WriteLine($"{context.Request.Method} {context.Request.Path}{context.Request.QueryString} - ORIGIN: {context.Request.Headers.Origin}");
                }
                catch (Exception e)
                {
                    logger?.WriteLine(e);
                }
            await next.Invoke();
        });
    }
    private static async Task<string> GetRequestBody(HttpRequest request)
    {
        if (!request.Body.CanSeek)
            request.EnableBuffering();
        request.Body.Position = 0;

        var rawRequestBody = await new StreamReader(request.Body).ReadToEndAsync();

        request.Body.Position = 0;

        return rawRequestBody;
    }
}
