using System.ComponentModel.DataAnnotations;
using System.Web;

namespace Notes.Interface.DTO;

public enum NoteChangeType
{
    Add,
    Update,
    Delete,

    /// <summary>
    /// Moves a note - and with it its whole subtree - to another place in the tree. Unlike
    /// delete + add this keeps the subnotes: a delete removes the subtree (and keeps it as trash),
    /// so re-adding only the note itself would lose everything below it.
    /// </summary>
    Move
}

public class NoteChange
{
    [Required]
    public required Guid NoteId { get; set; }
    [Required]
    public required NoteChangeType Type { get; set; }

    /// <summary>
    /// Necessary for Add and Update. A Delete may carry it for the revision check, and a Move
    /// never does - it moves the note the server already has.
    /// </summary>
    public NoteData? Data { get; set; }

    /// <summary>
    /// Optimistic concurrency (Update/Delete with data): the revision of the note this change was
    /// based on (Data.Rev before the edit bumped it). The server accepts the change only when the
    /// stored revision equals BaseRev; mismatches are rejected with HTTP 409 so stale clients
    /// cannot overwrite newer data. Null for legacy clients (accepted without a check).
    ///
    /// A Move needs no check: the note keeps its data, so it cannot overwrite anything.
    /// </summary>
    public ulong? BaseRev { get; set; }

    /// <summary>
    /// Necessary for Add and Move,
    /// but not for Update or Delete
    /// </summary>
    public Guid? ParentId { get; set; }
    /// <summary>
    /// Necessary for Add and Move,
    /// but not for Update or Delete
    /// </summary>
    public int? ChildInsertionIndex { get; set; }
}