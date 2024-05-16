using Notes.Interface;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Configuration
{
    public class ConfigData
    {
        // Local Gui Settings
        public Point Pos;
        public Size Size;
        public Color BackColor;

        // Notes payload
        public DateTime SaveTime;
        public List<Note> Notes;

        // Server
        public string ServerUri;
        public string ServerUsername;
        public string ServerPassword;

        public ConfigData()
        {
            Pos = new Point(-1, -1);
            Size = new Size(-1, -1);
            Notes = new List<Note>();
        }
    }
}
