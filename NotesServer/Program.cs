using Microsoft.AspNetCore.Mvc;
using Notes.Interface;
using System.Diagnostics;
using Notes.Server;
using System.Reflection;
//Console.WriteLine(Environment.GetEnvironmentVariable("NOTES_USERS"));

// --- Manage Paths ---------------------------------------------------------------------------------------------------------------------------------------

char _s = Path.DirectorySeparatorChar;
string localDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
string notesDir = $"{localDir}{_s}notes";
Directory.CreateDirectory(notesDir);
User.NotesDir = notesDir;
var logger = new ServerLogger(notesDir);

// --- Load Files and Env Vars ---------------------------------------------------------------------------------------------------------------------------------------

// Check for valid Environment
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NOTES_USERS")))
{
    logger.WriteLine($"NOTES_USERS is empty! Closing...");
    return;
}

// Create Users
List<User> users = new();
foreach (string userDef in Environment.GetEnvironmentVariable("NOTES_USERS").Split(' '))
{
    string[] userDefSplit = userDef.Split(':');
    var newUser = new User(userDefSplit[0], userDefSplit[1]);
    if (File.Exists(newUser.NotesJsonPath))
    {
        Payload? parsedPayload = null;
        try
        {
            parsedPayload = Payload.Parse(File.ReadAllText(newUser.NotesJsonPath), logger);
        }
        catch (Exception ex)
        {
            logger.WriteLine(ex);
        }
        if (parsedPayload != null)
            newUser.NotesPayload = parsedPayload;
    }
    users.Add(newUser);
}

// Create Login
var auth = new BasicAuth(users);

// --- Build API ---------------------------------------------------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseHttpsRedirection();

// --- Fill API  ---------------------------------------------------------------------------------------------------------------------------------------

app.MapGet("/hewwo", ([FromHeader(Name = "Authorization")] string? authTokenHeader) =>
    Results.Extensions.Html(@$"<!doctype html>
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
            {users.Select(x => $"<p>{x.Username}</p>").Combine("\n")}
        </body>
    </html>"));

app.MapGet("/notes", ([FromHeader(Name = "Authorization")] string? authTokenHeader, HttpRequest request) =>
    auth.GetUser(authTokenHeader, (User? u) => 
        Results.Ok(u?.NotesPayloadText)));

app.MapPost("/notes", async ([FromHeader(Name = "Authorization")] string? authTokenHeader, HttpRequest request) =>
{
    User? u;
    if ((u = auth.GetUser(authTokenHeader)) == null)
        return Results.Unauthorized();

    using StreamReader bodyStream = new(request.BodyReader.AsStream());
    string body = await bodyStream.ReadToEndAsync();
    Payload? bodyPayload = Payload.Parse(body);

    if (bodyPayload != null && 
        bodyPayload?.SaveTime > u.NotesPayload.SaveTime && 
        bodyPayload.Checksum == bodyPayload.GenerateChecksum())
    {
        logger.WriteLine($"Checksum check okay, writing to {u.NotesJsonPath}");

        u.NotesPayload = bodyPayload;
        File.WriteAllText(u.NotesJsonPath, u.NotesPayloadText);
    }

    return Results.Ok("Pog");
});

// --- Start ---------------------------------------------------------------------------------------------------------------------------------------

app.Run();