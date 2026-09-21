using Notes.Interface.DTO;

namespace Notes.Interface;

/// <summary>
/// The single place that decides how an incoming <see cref="NoteData"/> snapshot is applied over the
/// data the server already stores for a note (an Update change).
///
/// An Update carries a full snapshot, so a client that does not know a field - an older client - or
/// that has no value for it sends null, and storing that null loses a value the client never meant to
/// touch. For timestamps a client can only ever set, never clear (<see cref="NoteData.Created"/>,
/// <see cref="NoteData.StateLastChanged"/>), such a null is never an instruction, so the stored value
/// is kept. Everything else keeps plain snapshot behavior: Done, Canceled, Text, Hidden, Prio,
/// DueFrom/DueTo and the rest are values a user (or another client) can deliberately clear or empty,
/// and an incoming null must win there - that is the whole difference between the two groups.
///
/// Keeping the rule here, next to the DTO, means it is stated once, documented once and testable
/// without a server: a new set-once field needs one line in <see cref="Apply"/>, not another branch
/// at the write site.
/// </summary>
public static class NoteDataMerge
{
    /// <summary>
    /// Applies <paramref name="incoming"/> over <paramref name="stored"/> and returns the data to store
    /// for the note. <paramref name="incoming"/> is modified (it is the client's snapshot, already
    /// detached from whatever object it was deserialized into).
    /// </summary>
    public static NoteData Apply(NoteData incoming, NoteData stored)
    {
        // Set-once timestamps: a null means "this client has no value for it", never "clear it".
        // Further fields of that kind are added here - and nowhere else.
        incoming.Created ??= stored.Created;

        // StateLastChanged is also set-once, but it is derived from the state, so a null has two
        // meanings: the client does not know the field but left the state alone (keep the stored
        // stamp), or the client does not know the field and changed the state - then the state change
        // is happening now, and "now" (server clock, the only clock available here) is the honest
        // answer. A client that stamped the change itself always wins with its own value.
        bool stateChanged = incoming.Done != stored.Done || incoming.Canceled != stored.Canceled;
        incoming.StateLastChanged = incoming.StateLastChanged is { } clientStamp
            ? clientStamp
            : stateChanged
                ? DateTimeOffset.Now
                : stored.StateLastChanged;

        return incoming;
    }
}
