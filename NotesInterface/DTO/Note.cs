using System.Web;
using Newtonsoft.Json;

namespace Notes.Interface.DTO;

public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public NoteData Data { get; set; } = new NoteData();
    public List<Note> SubNotes { get; set; } = new();

    public static Note EmptyNote() => new Note();

    // Json Compat
    public bool? Done { get; set; }
    public string? Text { get; set; } // Should be URL encoded so that the json parser is not interrupted by special characters 
    [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public string? DecodedText
    {
        get
        {
            return HttpUtility.UrlDecode(Text) ?? "";
        }
        set { Text = HttpUtility.UrlEncode(value); }
    } // Should be URL encoded so that the json parser is not interrupted by special characters 
    public bool? Expanded { get; set; }
    public bool? Hidden { get; set; }
    public NotePriority? Prio { get; set; }

    public List<NotePosition> RecursiveSubNotes(bool SkipChildrenOfClosedNotes = false, int depth = 0, Note? parent = null)
    {
        List<NotePosition> result = [];
        result.Add(new NotePosition(depth, this, parent));
        if (SkipChildrenOfClosedNotes && !this.Data.Expanded)
            return result;
        foreach (var note in this.SubNotes)
        {
            result.AddRange(note.RecursiveSubNotes(SkipChildrenOfClosedNotes, depth + 1, this));
        }
        return result;
    }

    public void DeleteFrom(Note? Parent)
    {
        if (Parent != null)
        {
            Parent.SubNotes.Remove(this);
            if (Parent.SubNotes.Count == 0)
                Parent.Data.Expanded = false;
        }
    }

    public string SubtreeToStyledString() =>
        this.RecursiveSubNotes(SkipChildrenOfClosedNotes: true)
            .Select(x =>
            {
                var depthPadding = x.Depth <= 0 ? "" :
                    Enumerable
                        .Repeat("  ", x.Depth)
                        .Aggregate((x, y) => x + y);
                var expandedSymbol = x.Note.Data.Expanded ? "▼" : "▶";
                var noteText = x.Note.Data.Done ?
                    x.Note.Data.DecodedText.Select(x => x + "" + (char)822).Aggregate((x, y) => x + y) : // Cross through if done
                    x.Note.Data.DecodedText;

                return depthPadding + expandedSymbol + noteText;
            })
            .Aggregate((x, y) => x + "\n" + y);

    public override bool Equals(object? obj)
    {
        return obj is Note n && n.Id == this.Id;
    }
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}