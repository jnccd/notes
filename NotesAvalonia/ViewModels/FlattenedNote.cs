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
}