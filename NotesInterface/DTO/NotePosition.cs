using System.Web;

namespace Notes.Interface.DTO;

public record NotePosition(int Depth, Note Note, Note? Parent);