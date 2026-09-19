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

public static class NotesEndpointsV1
{
    const string ROUTE_VERSION_PREFIX = "/v1";
    static HttpClient httpClient = new();

    /// <summary>
    /// Adds a note to the user's payload. A change without a ParentId lands at the top level of the
    /// payload: the virtual root a client displays notes under is not a server-side note, so top level
    /// notes are expressed as "no parent". Returns the rejection to report, or null when it was added.
    /// </summary>
    public static (int Status, string Message)? TryAddNote(User user, NoteChange noteChange, List<NotePosition> allNotes)
    {
        if (allNotes.Any(x => x.Note.Id == noteChange.NoteId))
            return (StatusCodes.Status400BadRequest, $"Invalid Payload: Id {noteChange.NoteId} already exists in the notes structure");

        var placement = ResolveInsertPosition(user.NotesPayload!, allNotes, noteChange.ParentId, noteChange.ChildInsertionIndex);
        if (placement.Rejected is { } rejected)
            return rejected;

        placement.SubNotes!.Insert(placement.Index, new Note { Id = noteChange.NoteId, Data = noteChange.Data! });
        return null;
    }

    /// <summary>
    /// Moves a note - with its subtree - to the place the change names: the note is detached from its
    /// current parent (its subnotes come along) and inserted at the new parent and index.
    ///
    /// The destination is validated before anything is detached, so a rejected move leaves the note
    /// exactly where it was instead of dropping it out of the payload. Returns the rejection to
    /// report, or null when the note was moved.
    /// </summary>
    public static (int Status, string Message)? TryMoveNote(User user, NoteChange noteChange, NotePosition notePosition, List<NotePosition> allNotes)
    {
        var note = notePosition.Note;

        // Moving a note into itself (or into its own subtree) would detach the subtree containing the
        // insertion point and turn the payload into a cycle.
        if (ContainsNoteId(note, noteChange.ParentId))
            return (StatusCodes.Status400BadRequest, $"Invalid Payload: cannot move note {note.Id} into its own subtree");

        var placement = ResolveInsertPosition(user.NotesPayload!, allNotes, noteChange.ParentId, noteChange.ChildInsertionIndex);
        if (placement.Rejected is { } rejected)
            return rejected;

        RemoveNoteFromPayload(user.NotesPayload!, notePosition);
        placement.SubNotes!.Insert(placement.Index, note);
        return null;
    }

    static bool ContainsNoteId(Note note, Guid? id)
    {
        if (id == null)
            return false;
        if (note.Id == id.Value)
            return true;
        foreach (var subNote in note.SubNotes)
        {
            if (ContainsNoteId(subNote, id))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Finds the subnote list a change inserts into - the payload's own note list for a top level
    /// note - and validates the insertion index. Nothing is modified, so the result can be checked
    /// before a note is detached from where it currently is.
    /// </summary>
    static (List<Note>? SubNotes, int Index, (int Status, string Message)? Rejected) ResolveInsertPosition(
        Payload payload, List<NotePosition> allNotes, Guid? parentId, int? childInsertionIndex)
    {
        if (parentId == null)
        {
            int topLevelIndex = childInsertionIndex ?? payload.Notes.Count;
            if (topLevelIndex < 0 || topLevelIndex > payload.Notes.Count)
                return (null, 0, (StatusCodes.Status400BadRequest, $"Invalid Payload: ChildInsertionIndex {topLevelIndex} is out of bounds for the top level with {payload.Notes.Count} notes"));

            return (payload.Notes, topLevelIndex, null);
        }

        var noteParentPosition = allNotes.FirstOrDefault(x => x.Note.Id == parentId);
        if (noteParentPosition == null)
            return (null, 0, (StatusCodes.Status404NotFound, $"Parent note {parentId} not found!"));

        int index = childInsertionIndex ?? noteParentPosition.Note.SubNotes.Count;
        if (index < 0 || index > noteParentPosition.Note.SubNotes.Count)
            return (null, 0, (StatusCodes.Status400BadRequest, $"Invalid Payload: ChildInsertionIndex {index} is out of bounds for parent note {parentId} with {noteParentPosition.Note.SubNotes.Count} subnotes"));

        return (noteParentPosition.Note.SubNotes, index, null);
    }

    /// <summary>
    /// Keeps a note that is being deleted, together with its subtree, as trash - one row per note -
    /// and removes it from the active tree. Returns how many notes were kept.
    ///
    /// The trash is owned by the user passed in, which always comes from the authenticated user:
    /// nothing about the ownership can be influenced by the request, so a deleted note can only ever be
    /// read back by the user it belonged to.
    /// </summary>
    public static int TrashDeletedNote(NotesDbContext notesDbContext, User user, NotePosition notePosition)
    {
        var trash = DeletedNote.FromDeletedSubtree(notePosition.Note, user.UserId, notePosition.Parent?.Id);
        notesDbContext.DeletedNotes.AddRange(trash);
        RemoveNoteFromPayload(user.NotesPayload!, notePosition);
        return trash.Count;
    }

    /// <summary>
    /// Takes a note out of the active payload.
    ///
    /// A note at the top level of the payload has no parent, and <see cref="Note.DeleteFrom"/> does
    /// nothing when it is given none - so it has to be removed from the payload's own note list.
    /// Without that the payload is saved with the deleted note still in it, the next fetch brings it
    /// back, and every retry writes another set of trash rows.
    /// </summary>
    public static void RemoveNoteFromPayload(Payload payload, NotePosition notePosition)
    {
        if (notePosition.Parent == null)
            payload.Notes.Remove(notePosition.Note);
        else
            notePosition.Note.DeleteFrom(notePosition.Parent);
    }

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
                var allNotes = u!.NotesPayload!.GetAllNotes();
                var notePosition = allNotes.FirstOrDefault(x => x.Note.Id == noteChange.NoteId);

                switch (noteChange.Type)
                {
                    case NoteChangeType.Add:
                        if (noteChange.Data == null)
                        {
                            results[i] = new HttpResult(StatusCodes.Status400BadRequest, $"{i}: Invalid Payload: Add requires Data");
                            continue;
                        }
                        if (TryAddNote(u!, noteChange, allNotes) is { } rejected)
                        {
                            results[i] = new HttpResult(rejected.Status, $"{i}: {rejected.Message}");
                            continue;
                        }
                        break;
                    case NoteChangeType.Update:
                        if (noteChange.Data == null)
                        {
                            results[i] = new HttpResult(StatusCodes.Status400BadRequest, $"{i}: Invalid Payload: Update requires Data");
                            continue;
                        }
                        if (notePosition == null)
                        {
                            // Note no longer exists (deleted elsewhere): nothing to update.
                            break;
                        }
                        // Optimistic concurrency: accept the update only when it was based on the
                        // revision currently stored, so a stale client cannot overwrite newer data
                        // written by other clients. Legacy clients send no BaseRev -> accepted.
                        if (noteChange.BaseRev != null && notePosition.Note.Data.Rev != noteChange.BaseRev.Value)
                        {
                            results[i] = new HttpResult(StatusCodes.Status409Conflict, $"{i}: Update conflict for note {noteChange.NoteId}: server revision {notePosition.Note.Data.Rev} != base revision {noteChange.BaseRev.Value}");
                            continue; // leave server state and SaveTime untouched
                        }
                        notePosition.Note.Data = noteChange.Data;
                        break;
                    case NoteChangeType.Move:
                        if (notePosition == null)
                        {
                            // Not (or no longer) on the server: the queued Add that creates it uses the
                            // parent the client has now, so there is nothing to move.
                            Logger.WriteLine($"{i}: move for note {noteChange.NoteId} matched nothing on the server");
                            break;
                        }
                        if (TryMoveNote(u!, noteChange, notePosition, allNotes) is { } moveRejected)
                        {
                            results[i] = new HttpResult(moveRejected.Status, $"{i}: {moveRejected.Message}");
                            continue;
                        }
                        break;
                    case NoteChangeType.Delete:
                        // Optimistic concurrency on deletes too (when the client sent the deleted
                        // note's data): do not delete a note that was updated by a newer client in
                        // the meantime. Legacy/force deletes carry no BaseRev and stay unconditional
                        // (this is also the escape hatch for corrupted revisions).
                        if (noteChange.BaseRev != null && notePosition != null && notePosition.Note.Data.Rev != noteChange.BaseRev.Value)
                        {
                            results[i] = new HttpResult(StatusCodes.Status409Conflict, $"{i}: Delete conflict for note {noteChange.NoteId}: server revision {notePosition.Note.Data.Rev} != base revision {noteChange.BaseRev.Value}");
                            continue;
                        }
                        if (notePosition == null)
                        {
                            // Nothing to delete and nothing to keep: the note is not (or no longer) on
                            // the server - a note created locally whose Add never went through, for
                            // instance. Logged because the client only ever sees a plain success.
                            Logger.WriteLine($"{i}: delete for note {noteChange.NoteId} matched nothing on the server - nothing kept as trash");
                            break;
                        }
                        try
                        {
                            int trashed = TrashDeletedNote(notesDbContext, u!, notePosition);
                            Logger.WriteLine($"{i}: kept {trashed} note(s) as trash for deleted note {noteChange.NoteId}");
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
