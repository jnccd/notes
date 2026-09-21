using System;
using System.Text.Json.Serialization;
using System.Web;

namespace Notes.Interface.DTO;

public class NoteData
{
    public bool Done { get; set; } = false;
    /// <summary>
    /// Canceled acts like Done in the UI (crossed through etc.). If both Done and Canceled are
    /// set, Done wins for display purposes; the UI never produces that combination itself.
    /// </summary>
    public bool Canceled { get; set; } = false;
    /// <summary>
    /// When the note's state (Done/Canceled, see <see cref="SetState"/>) last changed: it was closed,
    /// reopened or switched between done and canceled. Null when the state never changed since this
    /// field existed, and for notes whose state was last changed before it existed - the state itself
    /// is known in both cases, only its change time is not. Stamped where the edit happens (so it
    /// works offline too) and travels inside the note's Data snapshot like every other field. Old
    /// payloads/clients simply ignore the field.
    /// </summary>
    public DateTimeOffset? StateLastChanged { get; set; }
    /// <summary>
    /// When the note was created. Null for notes created before this field existed; they are
    /// backfilled with the local "now" once when loaded (and synced via an update change).
    /// </summary>
    public DateTimeOffset? Created { get; set; }
    /// <summary>
    /// Symlink marker: when set, this note is a link to the note with this id (living elsewhere in
    /// the same payload tree). The link has no content of its own - the UI dereferences it to the
    /// target, edits the target's data and shows its children when the link is expanded. Old
    /// payloads/clients simply ignore the field.
    /// </summary>
    public Guid? LinkTargetId { get; set; }
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
    public NotePriority Prio { get; set; } = NotePriority.Medium;

    /// <summary>
    /// Optional timeframe the note is due in (see <see cref="DueTimeframe"/>). Both ends are optional:
    /// a timeframe with only one end is open on the other side, and no timeframe at all means the note
    /// is never "due". Old payloads/clients simply ignore the fields.
    /// </summary>
    public DateTimeOffset? DueFrom { get; set; }
    public DateTimeOffset? DueTo { get; set; }


    /// <summary>
    /// Optimistic-concurrency revision. Bumped by the editing client each time a change for this
    /// note is queued; sent inside the note's Data snapshot. The server accepts an Update only when
    /// its BaseRev matches the stored revision, so stale clients can never overwrite newer data.
    /// ulong: overflow/wraparound (and negative revisions) are practically impossible. Note for a
    /// future JS-based client: values above 2^53 lose integer precision in JSON.
    /// </summary>
    public ulong Rev { get; set; } = 0;

    /// <summary>
    /// Applies the note's Done/Canceled state in one step and stamps <see cref="StateLastChanged"/>
    /// when either flag actually changes.
    ///
    /// This is the only way the state is changed from an edit: writing <see cref="Done"/> or
    /// <see cref="Canceled"/> directly would change the state without saying when, and re-applying
    /// the state a note already has is a no-op here (a payload merged over the note must not fake a
    /// state change). Deserialization assigns the properties directly, which is exactly what keeps a
    /// stored <see cref="StateLastChanged"/> from being overwritten while a payload is read.
    /// </summary>
    public void SetState(bool done, bool canceled)
    {
        if (Done == done && Canceled == canceled)
            return;

        Done = done;
        Canceled = canceled;
        StateLastChanged = DateTimeOffset.Now;
    }
}