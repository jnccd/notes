using System;
using System.Collections.Generic;
using Avalonia.Controls.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NotesAvalonia.Models;

public enum NotePriority
{
    VeryHigh,
    High,
    Meduim,
    Low,
    VeryLow
}
public abstract class NoteData
{
    public NoteData()
    {
    }

    public NoteData(NoteData other)
    {
        this.Id = other.Id;
        this.Done = other.Done;
        this.Text = other.Text;
        this.Expanded = other.Expanded;
        this.Prio = other.Prio;
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Done { get; set; }
    public string? Text { get; set; }
    public bool Expanded { get; set; }
    public NotePriority Prio { get; set; }
}
public class Note : NoteData
{
    public List<Note> SubNotes { get; set; } = [];

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