using Notes.Interface.DTO;

namespace Notes.Interface;

/// <summary>
/// The one place a client derives changes from its local tree rather than creating them where the user
/// acted. Everything else stays where the edit happens: the view models queue their own Add, Update and
/// Delete changes as the note is created, edited, dragged, pasted or deleted (see the AddNoteChange
/// call sites in MainViewModel, NoteViewModel, MainView_NoteLogic and MainView_Reordering).
///
/// It exists for a single situation. A server payload says nothing about deletions, so a note the client
/// holds but the payload does not contain is either:
///
///   * a note this client created whose Add is still in flight - it belongs on the server, and so does
///     the subtree below it, which the server's single-note Add does not carry; or
///   * a note the server removed - another client deleted it, or this client's own delete has since
///     been delivered - and uploading it would undo that deletion.
///
/// Only the unsynced-change queue can tell those apart, so that is all this uses: a note with a queued
/// Add is being created, anything else the server does not have was removed. It never uploads a note the
/// server already has, never sends an Update, and never asks the server to delete anything - the changes
/// it does produce are Adds for notes under a creation the user already started.
/// </summary>
public static class LocalNoteSync
{
    /// <summary>
    /// What to do with the notes a client holds that the server's payload does not contain.
    /// </summary>
    /// <param name="Adds">Adds for the subtree below a note whose own creation is still queued, so that
    /// the whole thing the user created arrives. Parents come before children.</param>
    /// <param name="RemovedOnServer">Notes the server removed: the client drops its copies rather than
    /// uploading them back. Nothing the server has is ever listed here.</param>
    public sealed record LocalOnlyNotePlan(List<NoteChange> Adds, HashSet<Guid> RemovedOnServer);

    /// <summary>
    /// Decides what happens to the notes of a local tree that the server's payload does not contain.
    ///
    /// <paramref name="pendingAddIds"/> are the notes with an Add in the unsynced queue, i.e. the ones
    /// being created; <paramref name="handled"/> remembers what was queued before so nothing is queued
    /// twice.
    /// </summary>
    public static LocalOnlyNotePlan PlanLocalOnlyNotes(List<Note> notes, HashSet<Guid> serverIds, HashSet<Guid> pendingAddIds, ISet<Guid> handled)
    {
        var plan = new LocalOnlyNotePlan([], []);
        for (int i = 0; i < notes.Count; i++)
            Plan(notes[i], parentId: null, insertionIndex: i, parentIsNew: false, serverIds, pendingAddIds, handled, plan);
        return plan;
    }

    static void Plan(Note note, Guid? parentId, int insertionIndex, bool parentIsNew, HashSet<Guid> serverIds, HashSet<Guid> pendingAddIds, ISet<Guid> handled, LocalOnlyNotePlan plan)
    {
        bool onServer = serverIds.Contains(note.Id);
        bool addQueued = pendingAddIds.Contains(note.Id);

        if (!onServer && !addQueued && !parentIsNew)
        {
            // Nothing local is creating this note and the server does not have it: it was removed on
            // the server. Drop it (with its subtree) instead of uploading it back.
            plan.RemovedOnServer.Add(note.Id);
            return;
        }

        if (!onServer && !addQueued && handled.Add(note.Id))
        {
            // Part of a creation the user started: the note above it is on its way, but an Add inserts a
            // single note on the server, so this one needs its own change.
            plan.Adds.Add(new NoteChange
            {
                Type = NoteChangeType.Add,
                NoteId = note.Id,
                Data = note.Data,
                ParentId = parentId,
                ChildInsertionIndex = insertionIndex,
            });
        }

        // Below a note the server does not have yet, everything is part of the same creation.
        bool childrenAreNew = !onServer;
        for (int i = 0; i < note.SubNotes.Count; i++)
            Plan(note.SubNotes[i], note.Id, i, childrenAreNew, serverIds, pendingAddIds, handled, plan);
    }

    /// <summary>
    /// Removes the notes in <paramref name="noteIds"/> from a local tree, parents first. Returns whether
    /// anything was removed. This only touches the client's own copy.
    /// </summary>
    public static bool RemoveNotes(List<Note> notes, HashSet<Guid> noteIds)
    {
        bool removed = false;
        for (int i = notes.Count - 1; i >= 0; i--)
        {
            var note = notes[i];
            if (noteIds.Contains(note.Id))
            {
                notes.RemoveAt(i);
                removed = true;
                continue;
            }

            removed |= RemoveNotes(note.SubNotes, noteIds);
        }
        return removed;
    }
}
