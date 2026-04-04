using System.Web;
using Newtonsoft.Json;

namespace Notes.Interface;

public enum NotePriority
{
    VeryHigh,
    High,
    Medium,
    Low,
    VeryLow
}

public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Done { get; set; } = false;
    public string Text { get; set; } = ""; // Should be URL encoded so that the json parser is not interrupted by special characters 
    [JsonIgnore]
    public string DecodedText
    {
        get
        {
            return HttpUtility.UrlDecode(Text) ?? "";
        }
        set { Text = HttpUtility.UrlEncode(value); }
    } // Should be URL encoded so that the json parser is not interrupted by special characters 
    public bool Expanded { get; set; } = false;
    public bool Hidden { get; set; } = false;
    public List<Note> SubNotes { get; set; } = new();
    public NotePriority Prio { get; set; } = NotePriority.Medium;

    public static Note EmptyNote() => new Note()
    {
        Text = ""
    };

    public List<(int Depth, Note Note, Note? Parent)> RecursiveSubNotes(int depth = 0, Note? parent = null)
    {
        List<(int, Note, Note?)> result = [];
        result.Add((depth, this, parent));
        foreach (var note in this.SubNotes)
        {
            result.AddRange(note.RecursiveSubNotes(depth + 1, this));
        }
        return result;
    }

    public void DeleteFrom(Note? Parent)
    {
        if (Parent != null)
        {
            Parent.SubNotes.Remove(this);
            if (Parent.SubNotes.Count == 0)
                Parent.Expanded = false;
        }
    }

    public string SubtreeToStyledString()
    {
        return this.RecursiveSubNotes()
            .Select(x =>
            {
                var depthPadding = x.Depth <= 0 ? "" :
                    Enumerable
                        .Repeat("  ", x.Depth)
                        .Aggregate((x, y) => x + y);
                var expandedSymbol = x.Note.Expanded ? "▼" : "▶";
                var noteText = x.Note.Done ?
                    x.Note.DecodedText.Select(x => x + "" + (char)822).Aggregate((x, y) => x + y) : // Cross through if done
                    x.Note.DecodedText;

                return depthPadding + expandedSymbol + noteText;
            })
            .Aggregate((x, y) => x + "\n" + y);
    }

    public List<FlattenedNote> Flatten(uint depth = 0, FlattenedNote? parent = null)
    {
        List<FlattenedNote> result = [];
        var currentFlattened = new FlattenedNote(this) { Depth = depth, Parent = parent };
        result.Add(currentFlattened);
        if (Expanded)
            foreach (var note in this.SubNotes)
            {
                result.AddRange(note.Flatten(depth + 1, currentFlattened));
            }
        return result;
    }
    public override bool Equals(object? obj)
    {
        return obj is Note n && n.Id == this.Id;
    }
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}

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