using System;
using System.Web;
using Newtonsoft.Json;

namespace Notes.Interface.DTO;

public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public NoteData Data { get; set; } = new NoteData();
    public List<Note> SubNotes { get; set; } = new();

    /// <summary>Creates a brand new note and stamps its creation time (so the Created field is
    /// set immediately and travels with the note's Add change).</summary>
    public static Note EmptyNote()
    {
        var note = new Note();
        note.Data.Created = DateTimeOffset.Now;
        return note;
    }

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
                var noteText = (x.Note.Data.Done || x.Note.Data.Canceled) ?
                    x.Note.Data.DecodedText.Select(x => x + "" + (char)822).Aggregate((x, y) => x + y) : // Cross through if done/canceled
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