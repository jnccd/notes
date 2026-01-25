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
        public double? Width;
        public double? Height;
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
            Width = null;
            Height = null;
            SaveTime = DateTime.MinValue;
            Notes = [];
        }
    }
}
