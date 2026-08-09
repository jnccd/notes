using System.Web;
using Newtonsoft.Json;

namespace Notes.Interface.DTO;

public record NotePosition(int Depth, Note Note, Note? Parent);