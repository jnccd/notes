using System.ComponentModel.DataAnnotations;
using EzAuth;
using EzAuth.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Notes.Interface;
using Notes.Interface.DTO;
using NotesServer.Services.Auth;
using NotesServer.Services.Notes;

namespace NotesServer;

public static class NotesEndpoints
{
    const string ROUTE_VERSION_PREFIX = "/v1";
    static HttpClient httpClient = new();

    public static void RegisterNotesEndpoints(this IEndpointRouteBuilder routes, IServiceProvider services)
    {
        var version1Api = routes.MapGroup(ROUTE_VERSION_PREFIX);

        version1Api.MapGet("/authBackend", (
           IOptions<AuthOptions> authOptions) =>
        {
            return Results.Ok(new EzAuthAddress
            {
                RealmUrl = authOptions.Value.AuthBackendRealmUrl,
                Client = authOptions.Value.AuthBackendClient
            });
        });

        version1Api.MapGet("/notes", (
            [FromServices] AuthService auth,
            [FromHeader(Name = "Authorization")] string? authTokenHeader,
            HttpRequest request) =>
        {
            return auth?.GetUser(authTokenHeader, httpClient, u =>
            {
                return Results.Text(u.NotesPayload?.ToString(), contentType: "application/json");
            });
        });

        version1Api.MapPost("/notes/batch", async (
            [FromServices] AuthService auth,
            [FromServices] PersistenceService persistence,
            [FromHeader(Name = "Authorization"), Required] string? authTokenHeader,
            [FromBody, Required] NoteChange[] noteChanges,
            HttpRequest request) =>
        {
            Result<User> userResult = auth.GetUser(authTokenHeader, httpClient);
            if (!userResult.IsSuccess)
                return userResult.HttpResult;
            User? u = userResult.Value;

            (bool checkSuccessful, string errorMessage)[] checks = [];
            if (checks.Any(x => !x.checkSuccessful))
            {
                Logger.WriteLine($"Invalid post req received {checks.Select(x => x.ToString()).Aggregate((x, y) => x + ", " + y)} {checks.Where(x => !x.checkSuccessful).Select(x => x.errorMessage).Aggregate((x, y) => x + ", " + y)}");
                return Results.BadRequest($"Invalid Payload: {checks.FirstOrDefault(x => !x.checkSuccessful).errorMessage}");
            }

            Logger.WriteLine($"writing for {u?.UserId}");
            IResult[] results = new IResult[noteChanges.Length];

            for (int i = 0; i < noteChanges.Length; i++)
            {
                NoteChange noteChange = noteChanges[i];
                var notePosition = u!.NotesPayload!.GetAllNotes().FirstOrDefault(x => x.Note.Id == noteChange.NoteId);
                var noteParentPosition = u!.NotesPayload!.GetAllNotes().FirstOrDefault(x => x.Note.Id == noteChange.ParentId);

                switch (noteChange.Type)
                {
                    case NoteChangeType.Add:
                        if (noteChange.Data == null || noteChange.ParentId == null)
                        {
                            results[i] = Results.BadRequest($"{i}: Invalid Payload: Add requires Data and ParentId");
                            continue;
                        }
                        if (noteParentPosition == null)
                        {
                            results[i] = Results.NotFound($"{i}: Parent note {noteChange.ParentId} not found!");
                            continue;
                        }
                        if (noteChange.ChildInsertionIndex < 0 || noteChange.ChildInsertionIndex > noteParentPosition.Note.SubNotes.Count)
                        {
                            results[i] = Results.BadRequest($"{i}: Invalid Payload: ChildInsertionIndex {noteChange.ChildInsertionIndex} is out of bounds for parent note {noteChange.ParentId} with {noteParentPosition.Note.SubNotes.Count} subnotes");
                            continue;
                        }
                        try
                        {
                            noteParentPosition.Note.SubNotes.Insert(noteChange.ChildInsertionIndex ?? noteParentPosition.Note.SubNotes.Count, new Note
                            {
                                Id = noteChange.NoteId,
                                Data = noteChange.Data
                            });
                        }
                        catch (Exception e)
                        {
                            string message = $"{i}: Error adding note {noteChange.NoteId} to parent {noteChange.ParentId}: {e.Message}";
                            Logger.WriteLine(message);
                            results[i] = Results.BadRequest(message);
                            continue;
                        }
                        break;
                    case NoteChangeType.Update:
                        if (noteChange.Data == null)
                        {
                            results[i] = Results.BadRequest($"{i}: Invalid Payload: Update requires Data");
                            continue;
                        }
                        notePosition?.Note.Data = noteChange.Data;
                        break;
                    case NoteChangeType.Delete:
                        try
                        {
                            notePosition?.Note.DeleteFrom(notePosition.Parent);
                        }
                        catch (Exception e)
                        {
                            string message = $"{i}: Error deleting note {noteChange.NoteId} from parent {notePosition?.Parent?.Id}: {e.Message}";
                            Logger.WriteLine(message);
                            results[i] = Results.BadRequest(message);
                            continue;
                        }
                        break;
                }

                results[i] = Results.Ok();
                persistence.Save();
            }

            if (results.All(x => x is OkResult))
            {
                string message = $"User {u?.UserId} successfully applied {noteChanges.Length} changes";
                Logger.WriteLine(message);
                return Results.Json(new NotesBatchPostResult(results), statusCode: StatusCodes.Status200OK);
            }
            else if (results.All(x => x is not OkResult))
            {
                string message = $"All changes failed: {results.Select((x, i) => new { Result = x, Index = i }).Select(x => $"{x.Index}: {x.Result}").Aggregate((x, y) => x + ", " + y)}";
                Logger.WriteLine(message);
                return Results.Json(new NotesBatchPostResult(results), statusCode: StatusCodes.Status400BadRequest);
            }
            else
            {
                string message = $"One or more changes failed: {results.Select((x, i) => new { Result = x, Index = i }).Where(x => x.Result is not OkResult).Select(x => $"{x.Index}: {x.Result}").Aggregate((x, y) => x + ", " + y)}";
                Logger.WriteLine(message);
                return Results.Json(new NotesBatchPostResult(results), statusCode: StatusCodes.Status207MultiStatus);
            }
        });
    }
}
