using Notes.Interface.DTO;
using System.Collections.Generic;

namespace NotesAvalonia.Helper;

public static class Extensions
{
    public static List<FlattenedNote> Flatten(this Note note, uint depth = 0, FlattenedNote? parent = null)
    {
        List<FlattenedNote> result = [];
        var currentFlattened = new FlattenedNote(note) { Depth = depth, Parent = parent };
        result.Add(currentFlattened);
        if (note.Data.Expanded)
            foreach (var subNote in note.SubNotes)
            {
                result.AddRange(subNote.Flatten(depth + 1, currentFlattened));
            }
        return result;
    }
}