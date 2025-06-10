using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Notes.Interface;
using NotesServer.Services.BasicAuth;
using NotesServer.Services.Notes;

namespace NotesServer;

public static class NotesEndpoints
{
    public static void RegisterNotesEndpoints(this IEndpointRouteBuilder routes, IServiceProvider services)
    {
        routes.MapGet("/hewwo", (
            [FromServices] IBasicAuthService auth,
            [FromServices] PersistenceService persistence) =>
        {
            return Results.Extensions.Html(@$"<!doctype html>
                <html>
                    <head>
                        <title>Hewwo</title>
                        <style>
                            body {{font-family: sans-serif;}}
                        </style>
                    </head>
                    <body>
                        <h1>Hewwo Wowld :3</h1>
                        <h3>I know these users:</h3>
                        {persistence.Users?.Select(x => $"<p>{x.Username}</p>").Combine("\n")}
                    </body>
                </html>");
        });

        routes.MapGet("/notes", (
            [FromServices] IBasicAuthService auth,
            [FromHeader(Name = "Authorization")] string? authTokenHeader,
            HttpRequest request) =>
        {
            return auth?.GetUser(authTokenHeader, u =>
            {
                return Results.Text(u?.NotesPayload?.ToString(), contentType: "application/json");
            });
        });

        routes.MapPost("/notes", async (
            [FromServices] IBasicAuthService auth,
            [FromServices] PersistenceService persistence,
            [FromHeader(Name = "Authorization"), Required] string? authTokenHeader,
            //[FromBody, Required] Payload? bodyPayload,
            HttpRequest request) =>
        {
            User? u;
            if ((u = auth?.GetUser(authTokenHeader)) == null)
                return Results.Unauthorized();

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
                Logger.WriteLine($"Checksum check okay, writing for {u.Username}");

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
