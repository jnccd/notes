using System.Web;
using Newtonsoft.Json;

namespace Notes.Interface.DTO;

public class FlattenedNote
{
    public FlattenedNote(Note OriginalNote)
    {
        this.OriginalNote = OriginalNote;
    }
    public uint Depth { get; set; }
    public FlattenedNote? Parent { get; set; }
    public Note OriginalNote { get; set; }
    /// <summary>
    /// For a row that is a symlink (OriginalNote.Data.LinkTargetId set) this is the resolved
    /// target note whose content the row displays. Null for regular notes. Children shown under an
    /// expanded link have their own regular entries whose Parent points at the link's target.
    /// </summary>
    public Note? DereferencedNote { get; set; }
}