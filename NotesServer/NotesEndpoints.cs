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
            [FromServices] NotesDbContext notesDbContext,
            [FromHeader(Name = "Authorization")] string? authTokenHeader,
            HttpRequest request) =>
        {
            return auth?.GetUser(authTokenHeader, httpClient, notesDbContext, u =>
            {
                return Results.Text(u.NotesPayload?.ToString(), contentType: "application/json");
            });
        });

        version1Api.MapPost("/notes/batch", async (
            [FromServices] AuthService auth,
            [FromServices] NotesDbContext notesDbContext,
            [FromHeader(Name = "Authorization"), Required] string? authTokenHeader,
            [FromBody, Required] NoteChange[] noteChanges,
            HttpRequest request) =>
        {
            Result<User> userResult = auth.GetUser(authTokenHeader, httpClient, notesDbContext);
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
            HttpResult[] results = new HttpResult[noteChanges.Length];

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
                            results[i] = new HttpResult(StatusCodes.Status400BadRequest, $"{i}: Invalid Payload: Add requires Data and ParentId");
                            continue;
                        }
                        if (noteParentPosition == null)
                        {
                            results[i] = new HttpResult(StatusCodes.Status404NotFound, $"{i}: Parent note {noteChange.ParentId} not found!");
                            continue;
                        }
                        if (noteChange.ChildInsertionIndex < 0 || noteChange.ChildInsertionIndex > noteParentPosition.Note.SubNotes.Count)
                        {
                            results[i] = new HttpResult(StatusCodes.Status400BadRequest, $"{i}: Invalid Payload: ChildInsertionIndex {noteChange.ChildInsertionIndex} is out of bounds for parent note {noteChange.ParentId} with {noteParentPosition.Note.SubNotes.Count} subnotes");
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
                            results[i] = new HttpResult(StatusCodes.Status400BadRequest, message);
                            continue;
                        }
                        break;
                    case NoteChangeType.Update:
                        if (noteChange.Data == null)
                        {
                            results[i] = new HttpResult(StatusCodes.Status400BadRequest, $"{i}: Invalid Payload: Update requires Data");
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
                            results[i] = new HttpResult(StatusCodes.Status400BadRequest, message);
                            continue;
                        }
                        break;
                }

                results[i] = new HttpResult(StatusCodes.Status200OK);
                u!.NotesPayload!.Checksum = u!.NotesPayload!.GenerateChecksum();
                u!.NotesPayload!.SaveTime = DateTime.Now;
                notesDbContext.SaveChanges();
            }

            if (results.All(x => x.StatusCode == StatusCodes.Status200OK))
            {
                string message = $"User {u?.UserId} successfully applied {noteChanges.Length} changes";
                Logger.WriteLine(message);
                return Results.Json(new NotesBatchPostResult(results.ToArray()), statusCode: StatusCodes.Status200OK);
            }
            else if (results.All(x => x.StatusCode != StatusCodes.Status200OK))
            {
                string message = $"All changes failed: {results.Select((x, i) => new { Result = x, Index = i }).Select(x => $"{x.Index}: {x.Result}").Aggregate((x, y) => x + ", " + y)}";
                Logger.WriteLine(message);
                return Results.Json(new NotesBatchPostResult(results), statusCode: StatusCodes.Status400BadRequest);
            }
            else
            {
                string message = $"One or more changes failed: {results.Select((x, i) => new { Result = x, Index = i }).Where(x => x.Result.StatusCode != StatusCodes.Status200OK).Select(x => $"{x.Index}: {x.Result}").Aggregate((x, y) => x + ", " + y)}";
                Logger.WriteLine(message);
                return Results.Json(new NotesBatchPostResult(results), statusCode: StatusCodes.Status207MultiStatus);
            }
        });
    }
}
