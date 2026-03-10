using Avalonia;
using Avalonia.Media;
using Notes.Interface;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Size = Avalonia.Size;

namespace NotesAvalonia.Configuration
{
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
        public string? KeycloakRefreshToken;
        public string? KeycloakRefreshTokenForAndroidWidget;

        // Notes payload
        public Dictionary<string, NotePayload> NotePayloadOfUser;

        public NotePayload? CurrentUsersNotePayload()
        {
            if (string.IsNullOrWhiteSpace(Username))
                return null;

            if (!NotePayloadOfUser.ContainsKey(Username))
                NotePayloadOfUser[Username!] = new();

            return NotePayloadOfUser[Username];
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
