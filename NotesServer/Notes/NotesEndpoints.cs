using Microsoft.AspNetCore.Mvc;
using Notes.Interface;
using Notes.Server;
using NotesServer.BasicAuth;

namespace NotesServer.Notes
{
    public static class NotesEndpoints
    {
        public static void RegisterNotesEndpoints(this IEndpointRouteBuilder routes, IServiceProvider services)
        {
            routes.MapGet("/hewwo", (
                [FromServices] IBasicAuthService auth,
                [FromServices] INotesEnvironmentService notesEnv) =>
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
                        {notesEnv?.Users?.Select(x => $"<p>{x.Username}</p>").Combine("\n")}
                    </body>
                </html>");
            });

            routes.MapGet("/notes", (
                [FromServices] IBasicAuthService auth,
                [FromServices] INotesEnvironmentService notesEnv,
                [FromHeader(Name = "Authorization")] string? authTokenHeader,
                HttpRequest request) =>
            {
                return auth?.GetUser(authTokenHeader, (User? u) =>
                {
                    return Results.Text(u?.NotesPayloadText, contentType: "application/json");
                });
            });

            routes.MapPost("/notes", async (
                [FromServices] IBasicAuthService auth,
                [FromServices] INotesEnvironmentService notesEnv,
                [FromHeader(Name = "Authorization")] string? authTokenHeader, 
                HttpRequest request) =>
            {
                User? u;
                if ((u = auth?.GetUser(authTokenHeader)) == null)
                    return Results.Unauthorized();

                using StreamReader bodyStream = new(request.BodyReader.AsStream());
                string body = await bodyStream.ReadToEndAsync();
                Payload? bodyPayload = Payload.Parse(body);

                if (bodyPayload != null &&
                    bodyPayload?.SaveTime > u?.NotesPayload?.SaveTime &&
                    bodyPayload.Checksum == bodyPayload.GenerateChecksum())
                {
                    var writePath = notesEnv?.UsersNotesJsonPath(u);
                    Logger.WriteLine($"Checksum check okay, writing to {writePath}");

                    u.NotesPayload = bodyPayload;
                    if (writePath != null)
                        File.WriteAllText(writePath, u.NotesPayloadText);
                }
                else
                    Logger.WriteLine($"invalid post req recieved " +
                        $"{bodyPayload != null} {bodyPayload?.SaveTime > u?.NotesPayload?.SaveTime} {bodyPayload?.Checksum == bodyPayload?.GenerateChecksum()}");

                return Results.Ok("Pog");
            });
        }
    }
}
