using Avalonia;
using Avalonia.Media;
using Notes.Interface;
using Notes.Interface.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Size = Avalonia.Size;

namespace NotesAvalonia.Configuration
{
    public record NotesDataForUser(NotePayload NotePayload, List<NoteChange> UnsyncedChanges);

    public class ConfigData
    {
        // Local Gui Settings
        // Pos/BackColor stay fields with [JsonInclude] instead of auto-properties: their types are
        // structs whose members STJ can only assign by whole-value field/ctor write, and the fields
        // keep the stored JSON shape identical to the Newtonsoft era. The converters are also
        // declared here so the source generator treats them as handled.
        [JsonInclude]
        [JsonConverter(typeof(PixelPointJsonConverter))]
        public PixelPoint? Pos;
        [JsonInclude]
        [JsonConverter(typeof(ColorJsonConverter))]
        public Color BackColor;
        public double? Width { get; set; }
        public double? Height { get; set; }

        // Server
        public string? ServerUri { get; set; }
        public string? Username { get; set; }
        public string? AuthBackendRefreshToken { get; set; }
        public string? AuthBackendRefreshTokenForAndroidWidget { get; set; }

        // Notes payload
        public Dictionary<string, NotesDataForUser> NotePayloadOfUser { get; set; }

        // Workspace key used while no user is logged in yet (empty/whitespace Username). Notes
        // typed in that state used to be global in Config.Data.Notes and persisted regardless of
        // login; giving the anonymous session its own payload entry keeps that behavior - notes
        // survive restarts instead of being lost because there is no user payload to store them in.
        public const string AnonymousUserKey = "";

        [JsonIgnore]
        public string EffectiveUserKey =>
            string.IsNullOrWhiteSpace(Username) ? AnonymousUserKey : Username!;

        /// <summary>
        /// Queues a change for the current user.
        /// Update changes carry a full <see cref="NoteData"/> snapshot: only the newest update per
        /// note matters (typing one character must not enqueue one update), and each update stamps
        /// the note's optimistic-concurrency revision (BaseRev = revision it was based on, then the
        /// note's Rev is bumped) so the server can reject stale clients.
        /// </summary>
        public void AddNoteChange(NoteChange change)
        {
            var queue = CurrentUsersUnsyncedChanges;
            if (queue == null)
                return;

            if (change.Type == NoteChangeType.Update)
            {
                // Coalesce: replace the pending snapshot of the same note in place and keep the
                // entry's original BaseRev/Rev - a burst of local edits advances the revision
                // exactly once per update that will actually be sent.
                int existingIndex = queue.FindIndex(c => c.Type == NoteChangeType.Update && c.NoteId == change.NoteId);
                if (existingIndex >= 0)
                {
                    var existing = queue[existingIndex];
                    if (change.Data != null)
                        existing.Data = change.Data;
                    return;
                }

                // Fresh update: stamp the revision this change builds on and the new revision on
                // the note itself (rides the snapshot that gets sent and persisted).
                if (change.Data != null)
                {
                    change.BaseRev = change.Data.Rev;
                    change.Data.Rev += 1;
                }
            }
            else if (change.Type == NoteChangeType.Delete && change.Data != null)
            {
                // A delete that carries the note's data gets an optimistic-concurrency check too
                // (BaseRev = the revision being deleted), so a stale client cannot delete a note
                // that was updated by a newer client in the meantime. Deletes without data are
                // unconditional (legacy clients / deliberate force-delete escape hatch).
                change.BaseRev = change.Data.Rev;
            }

            queue.Add(change);
        }

        /// <summary>
        /// Removes redundant entries from every user's unsynced-change queue. Only the newest
        /// Update per note is meaningful (updates are full snapshots), so older ones are dropped;
        /// all Adds/Deletes are kept untouched.
        /// </summary>
        public void CompactUnsyncedChanges()
        {
            foreach (var perUser in NotePayloadOfUser)
            {
                var queue = perUser.Value.UnsyncedChanges;
                if (queue == null || queue.Count <= 1)
                    continue;

                // Scan backwards keeping the newest Update per note, then reverse back.
                var newestFirst = new List<NoteChange>(queue.Count);
                var seenUpdateIds = new HashSet<Guid>();
                for (int i = queue.Count - 1; i >= 0; i--)
                {
                    var change = queue[i];
                    if (change.Type == NoteChangeType.Update && !seenUpdateIds.Add(change.NoteId))
                        continue;
                    newestFirst.Add(change);
                }
                newestFirst.Reverse();

                queue.Clear();
                queue.AddRange(newestFirst);
            }
        }

        public NotePayload? CurrentUsersNotePayload()
        {
            var userKey = EffectiveUserKey;

            if (!NotePayloadOfUser.ContainsKey(userKey))
                NotePayloadOfUser[userKey] = new(NotePayload: new(), UnsyncedChanges: new());

            return NotePayloadOfUser[userKey].NotePayload;
        }
        [JsonIgnore]
        public List<NoteChange>? CurrentUsersUnsyncedChanges
        {
            get
            {
                var userKey = EffectiveUserKey;

                if (!NotePayloadOfUser.ContainsKey(userKey))
                    NotePayloadOfUser[userKey] = new(NotePayload: new(), UnsyncedChanges: new());

                return NotePayloadOfUser[userKey].UnsyncedChanges;
            }
            set
            {
                var userKey = EffectiveUserKey;

                if (!NotePayloadOfUser.ContainsKey(userKey))
                    NotePayloadOfUser[userKey] = new(NotePayload: new(), UnsyncedChanges: new());

                NotePayloadOfUser[userKey].UnsyncedChanges.Clear();
                NotePayloadOfUser[userKey].UnsyncedChanges.AddRange(value ?? new());
            }
        }

        public ConfigData()
        {
            Pos = null;
            Width = null;
            Height = null;
            NotePayloadOfUser = new();
        }
    }

    public record NotePayload
    {
        public DateTime SaveTime { get; set; }
        public List<Note> Notes { get; set; }

        public NotePayload()
        {
            SaveTime = DateTime.MinValue;
            Notes = [];
        }
    }
}
