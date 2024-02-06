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

        public Payload Payload;

        public ConfigData()
        {
            Payload = new Payload(new DateTime(2000, 1, 1), new List<Note>());
        }
    }
}
