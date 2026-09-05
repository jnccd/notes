using System.ComponentModel.DataAnnotations;
using System.Web;

namespace Notes.Interface.DTO;

public enum NoteChangeType
{
    Add,
    Update,
    Delete
}

public class NoteChange
{
    [Required]
    public required Guid NoteId { get; set; }
    [Required]
    public required NoteChangeType Type { get; set; }

    /// <summary>
    /// Necessary for Add and Update, 
    /// but not for Delete
    /// </summary>
    public NoteData? Data { get; set; }

    /// <summary>
    /// Optimistic concurrency (Update/Delete with data): the revision of the note this change was
    /// based on (Data.Rev before the edit bumped it). The server accepts the change only when the
    /// stored revision equals BaseRev; mismatches are rejected with HTTP 409 so stale clients
    /// cannot overwrite newer data. Null for legacy clients (accepted without a check).
    /// </summary>
    public ulong? BaseRev { get; set; }

    /// <summary>
    /// Necessary for Add, 
    /// but not for Update or Delete
    /// </summary>
    public Guid? ParentId { get; set; }
    /// <summary>
    /// Necessary for Add, 
    /// but not for Update or Delete
    /// </summary>
    public int? ChildInsertionIndex { get; set; }
}