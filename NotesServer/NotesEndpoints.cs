using System.ComponentModel.DataAnnotations;
using EzKeycloak;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Notes.Interface;
using NotesServer.Services.Auth;
using NotesServer.Services.Notes;

namespace NotesServer;

public static class NotesEndpoints
{
    static HttpClient httpClient = new();

    public static void RegisterNotesEndpoints(this IEndpointRouteBuilder routes, IServiceProvider services)
    {
        routes.MapGet("/keycloak", (
           IOptions<AuthOptions> authOptions) =>
        {
            return Results.Ok(new KeyCloakAddress
            {
                KeycloakRealmUrl = authOptions.Value.KeycloakRealmUrl,
                KeycloakClient = authOptions.Value.KeycloakClient
            });
        });

        routes.MapGet("/", (
            [FromServices] AuthService auth,
            [FromHeader(Name = "Authorization")] string? authTokenHeader,
            HttpRequest request) =>
        {
            return auth?.GetUser(authTokenHeader, httpClient, u =>
            {
                return Results.Text(u.NotesPayload?.ToString(), contentType: "application/json");
            });
        });

        routes.MapPost("/", async (
            [FromServices] AuthService auth,
            [FromServices] PersistenceService persistence,
            [FromHeader(Name = "Authorization"), Required] string? authTokenHeader,
            //[FromBody, Required] Payload? bodyPayload,
            HttpRequest request) =>
        {
            Result<User> userResult = auth.GetUser(authTokenHeader, httpClient);
            if (!userResult.IsSuccess)
                return userResult.HttpResult;
            User? u = userResult.Value;

            using StreamReader bodyStream = new(request.BodyReader.AsStream());
            string body = await bodyStream.ReadToEndAsync();
            Payload? bodyPayload = Payload.Parse(body);

            bool[] checks = [bodyPayload != null,
                (bodyPayload?.SaveTime > u?.NotesPayload?.SaveTime || u?.NotesPayload == null),
                bodyPayload?.SaveTime <= DateTime.Now.AddSeconds(3),
                bodyPayload?.Checksum == bodyPayload?.GenerateChecksum()
            ];
            if (checks.All(x => x))
            {
                Logger.WriteLine($"Checksum check okay, writing for {u?.Username}");

                if (u != null)
                    u.NotesPayload = bodyPayload;
                persistence.Save();
            }
            else
            {
                Logger.WriteLine($"Invalid post req received " +
                    checks.Select(x => x.ToString()).Aggregate((x, y) => x + ", " + y));
                return Results.BadRequest("Invalid Payload " +
                    checks.Select(x => x.ToString()).Aggregate((x, y) => x + ", " + y));
            }

            return Results.Ok("Pog");
        });
    }
}
