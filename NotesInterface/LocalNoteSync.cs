using Notes.Interface.DTO;

namespace Notes.Interface;

/// <summary>
/// Turns a client's local note tree into the sync changes that express it.
/// </summary>
public static class LocalNoteSync
{
    /// <summary>
    /// The parent id a change should reference for a note, where null means "the top level of the
    /// payload".
    ///
    /// A client shows notes under a virtual root, but that root is a local display construct: it is
    /// recreated on every start and the server has no note for it. Sending its id as a ParentId made
    /// every note added at the top level fail with "Parent note not found", which is why notes created
    /// at the top level never reached the server at all.
    /// </summary>
    public static Guid? ServerParentIdOf(Note? parent, Note? virtualRoot) =>
        parent == null || ReferenceEquals(parent, virtualRoot) ? null : parent.Id;

    /// <summary>
    /// Add changes for the notes that exist only locally, in pre-order so every note's parent is
    /// created before it.
    ///
    /// The server's Add inserts a single note and does not carry a subtree, so every note of a
    /// local-only subtree needs its own change - including top level ones.
    ///
    /// <paramref name="handled"/> is both the "these already have a change coming" set for this call and
    /// the memory of what was queued before; the caller keeps it across calls so nothing is queued
    /// twice. Notes already known to the server are skipped but their children are still examined.
    /// </summary>
    public static List<NoteChange> BuildLocalOnlyAdds(List<Note> notes, HashSet<Guid> serverIds, ISet<Guid> handled)
    {
        var changes = new List<NoteChange>();
        for (int i = 0; i < notes.Count; i++)
            BuildLocalOnlyAdd(notes[i], parentId: null, insertionIndex: i, serverIds, handled, changes);
        return changes;
    }

    static void BuildLocalOnlyAdd(Note note, Guid? parentId, int insertionIndex, HashSet<Guid> serverIds, ISet<Guid> handled, List<NoteChange> changes)
    {
        if (serverIds.Contains(note.Id))
        {
            // Already on the server, so only notes below it can be missing.
            for (int i = 0; i < note.SubNotes.Count; i++)
                BuildLocalOnlyAdd(note.SubNotes[i], note.Id, i, serverIds, handled, changes);
            return;
        }

        if (handled.Add(note.Id))
        {
            changes.Add(new NoteChange
            {
                Type = NoteChangeType.Add,
                NoteId = note.Id,
                Data = note.Data,
                ParentId = parentId,
                ChildInsertionIndex = insertionIndex,
            });
        }

        // The subtree is queued whether or not this note's own change went in - a change may already
        // be queued for it, and its children still need theirs.
        for (int i = 0; i < note.SubNotes.Count; i++)
            BuildLocalOnlyAdd(note.SubNotes[i], note.Id, i, serverIds, handled, changes);
    }
}
