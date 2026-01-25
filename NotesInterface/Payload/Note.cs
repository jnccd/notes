namespace Notes.Interface;

public enum NotePriority
{
    VeryHigh,
    High,
    Meduim,
    Low,
    VeryLow
}

public class Note
{
    public bool Done { get; set; } = false;
    public string Text { get; set; } = "";
    public bool Expanded { get; set; } = false;
    public List<Note> SubNotes { get; set; } = new();
    public NotePriority Prio { get; set; } = NotePriority.Meduim;

    public static Note EmptyNote() => new Note()
    {
        Text = " "
    };

    public List<Note> RecursiveSubnotes()
    {
        List<Note> result = [];
        result.Add(this);
        foreach (var note in this.SubNotes)
        {
            result.AddRange(note.RecursiveSubnotes());
        }
        return result;
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