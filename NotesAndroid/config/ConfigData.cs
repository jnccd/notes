using Notes.Interface;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Configuration
{
    public class ConfigData
    {
        public string ServerUri;
        public string ServerUsername;
        public string ServerPassword;

        public DateTime SaveTime;
        public List<Note> Notes;

        public ConfigData()
        {
            Notes = new List<Note>();
        }
    }
}
