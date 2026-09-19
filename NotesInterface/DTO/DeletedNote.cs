using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Notes.Interface.DTO;

/// <summary>
/// A note that was removed from the active tree, kept so its content is not lost.
///
/// One row per deleted note. The subtree a deletion removes is kept as rows linked through
/// <see cref="ParentDeletedNoteId"/> - a table with a foreign key to itself - so every deleted note is
/// its own row, owned by the user it belonged to. Trash is not reachable by the client yet; it only has
/// to be retained, and to never be readable across users (see NotesServer's NotesDbContext).
/// </summary>
public class DeletedNote
{
    /// <summary>Identifier of this trash entry.</summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The deletion this note was part of. Every note removed by one delete change shares it, so a
    /// whole deletion can still be found (and later restored or purged) as a unit.
    /// </summary>
    public Guid DeletionId { get; set; }

    /// <summary>Id the note had in the active tree, so a trash entry stays traceable to it.</summary>
    public Guid NoteId { get; set; }

    /// <summary>
    /// The user the note belonged to. Required and always set from the authenticated user, never from
    /// the request body, so a deleted note can only ever be read back by its owner.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// The trash entry of this note's parent within the deleted subtree; null on the note that was
    /// actually deleted. The children of the deleted note (and of them, recursively) point here.
    /// </summary>
    public Guid? ParentDeletedNoteId { get; set; }

    /// <summary>Id of the note's parent in the live tree at the time of deletion, when it had one.</summary>
    public Guid? OriginalParentId { get; set; }

    /// <summary>When the note was deleted, in UTC.</summary>
    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;

    // The note's own data, stored as jsonb: it matches how note data is stored inside the active
    // payload, and keeps this table from needing a migration every time NoteData gains a field.
    public string? DataJson { get; set; }

    [NotMapped]
    public NoteData Data
    {
        get => DataJson == null ? new NoteData() : JsonSerializer.Deserialize<NoteData>(DataJson, NoteJson.Default) ?? new NoteData();
        set => DataJson = JsonSerializer.Serialize(value, NoteJson.Default);
    }

    /// <summary>
    /// Snapshots a note and the subtree that is removed with it, as one row per note owned by
    /// <paramref name="userId"/>. Data is serialized here, so the entries keep every note exactly as it
    /// was at deletion time.
    /// </summary>
    public static List<DeletedNote> FromDeletedSubtree(Note note, string userId, Guid? originalParentId)
    {
        var deletionId = Guid.NewGuid();
        var deletedNotes = new List<DeletedNote>();
        AddNoteAndSubNotes(deletedNotes, note, userId, deletionId, originalParentId, parentDeletedNoteId: null);
        return deletedNotes;
    }

    static void AddNoteAndSubNotes(List<DeletedNote> deletedNotes, Note note, string userId, Guid deletionId, Guid? originalParentId, Guid? parentDeletedNoteId)
    {
        var deletedNote = new DeletedNote
        {
            DeletionId = deletionId,
            NoteId = note.Id,
            UserId = userId,
            ParentDeletedNoteId = parentDeletedNoteId,
            OriginalParentId = originalParentId,
            Data = note.Data,
        };
        deletedNotes.Add(deletedNote);

        foreach (var subNote in note.SubNotes)
            AddNoteAndSubNotes(deletedNotes, subNote, userId, deletionId, note.Id, deletedNote.Id);
    }
}
