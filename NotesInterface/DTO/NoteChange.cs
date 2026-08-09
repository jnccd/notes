using System.ComponentModel.DataAnnotations;
using System.Web;
using Newtonsoft.Json;

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