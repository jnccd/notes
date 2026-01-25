using Avalonia;
using Avalonia.Media;
using Notes.Interface;
using System;
using System.Collections.Generic;
using Size = Avalonia.Size;

namespace NotesAvalonia.Configuration
{
    public class ConfigData
    {
        // Local Gui Settings
        public PixelPoint? Pos;
        public Size? Size;
        public Color BackColor;

        // Server
        public string? ServerUri;
        public string? ServerUsername;
        public string? ServerPassword;

        // Notes payload
        public DateTime SaveTime;
        public List<Note> Notes;

        public ConfigData()
        {
            Pos = null;
            Size = null;
            SaveTime = DateTime.MinValue;
            Notes = new List<Note>() { new Note() };
        }
    }
}
