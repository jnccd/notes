using Notes.Interface;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace NotesAndroid.Configuration
{
    public class ConfigData
    {
        // Local Gui Settings
        public Point Pos;
        public Size Size;
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
            SaveTime = DateTime.MinValue;
            Notes = new List<Note>() { new Note() };
        }
    }
}
