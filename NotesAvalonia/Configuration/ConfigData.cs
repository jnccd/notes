using Avalonia;
using Avalonia.Media;
using Notes.Interface;
using Notes.Interface.DTO;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Size = Avalonia.Size;

namespace NotesAvalonia.Configuration
{
    public record NotesDataForUser(NotePayload NotePayload, List<NoteChange> UnsyncedChanges);

    public class ConfigData
    {
        // Local Gui Settings
        public PixelPoint? Pos;
        public double? Width;
        public double? Height;
        public Color BackColor;

        // Server
        public string? ServerUri;
        public string? Username;
        public string? AuthBackendRefreshToken;
        public string? AuthBackendRefreshTokenForAndroidWidget;

        // Notes payload
        public Dictionary<string, NotesDataForUser> NotePayloadOfUser;

        public NotePayload? CurrentUsersNotePayload()
        {
            if (string.IsNullOrWhiteSpace(Username))
                return null;

            if (!NotePayloadOfUser.ContainsKey(Username))
                NotePayloadOfUser[Username!] = new(NotePayload: new(), UnsyncedChanges: new());

            return NotePayloadOfUser[Username].NotePayload;
        }
        public List<NoteChange>? CurrentUsersUnsyncedChanges
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Username))
                    return null;

                if (!NotePayloadOfUser.ContainsKey(Username))
                    NotePayloadOfUser[Username!] = new(NotePayload: new(), UnsyncedChanges: new());

                return NotePayloadOfUser[Username].UnsyncedChanges;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(Username))
                    return;

                if (!NotePayloadOfUser.ContainsKey(Username))
                    NotePayloadOfUser[Username!] = new(NotePayload: new(), UnsyncedChanges: new());

                NotePayloadOfUser[Username].UnsyncedChanges.Clear();
                NotePayloadOfUser[Username].UnsyncedChanges.AddRange(value ?? new());
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
        public DateTime SaveTime;
        public List<Note> Notes;

        public NotePayload()
        {
            SaveTime = DateTime.MinValue;
            Notes = [];
        }
    }
}
