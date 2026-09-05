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
        /// Queues a change for the current user. Update changes carry a full <see cref="NoteData"/>
        /// snapshot, so a new update supersedes any earlier update of the same note still in the
        /// queue - without this, typing one character enqueues one update and the queue grows
        /// without bound while changes cannot be delivered (offline / not logged in yet).
        /// </summary>
        public void AddNoteChange(NoteChange change)
        {
            var queue = CurrentUsersUnsyncedChanges;
            if (queue == null)
                return;

            if (change.Type == NoteChangeType.Update)
                queue.RemoveAll(c => c.Type == NoteChangeType.Update && c.NoteId == change.NoteId);

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
