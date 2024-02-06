using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notes.Interface
{
    public class Payload
    {
        public DateTime SaveTime;

        public string Source;
        public long Checksum;

        public List<Note> Notes;

        public Payload(DateTime saveTime, List<Note> notes)
        {
            SaveTime = saveTime;
            Notes = notes;
            Source = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

            Checksum = GenerateChecksum();
        }

        public void Update()
        {
            SaveTime = DateTime.Now;
            Checksum = GenerateChecksum();
            Source = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        }
        public int GenerateChecksum() => SaveTime.Minute + SaveTime.Second +
            Encoding.Unicode.GetBytes(Notes.Select(x => x.Text).Combine("")).Select(x => (int)x).Sum();

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(JsonConvert.SerializeObject(this, Formatting.Indented));
            sb.Replace("\\\n", "");
            sb.Replace("\\n", "");
            sb.Replace("\\\"", "\"");
            sb.Replace("\\\"", "\"");
            //sb.Replace("\r", "");
            //sb.Replace("\n", "");
            //sb.Replace("\\", "");
            return sb.ToString();
        }
        public static Payload? Parse(string json, Logger? logger = null)
        {
            try
            {
                return JsonConvert.DeserializeObject<Payload>(json);
            }
            catch 
            {
                logger?.WriteLine($"Error parsing payload {json}");
                return null; 
            }
        }
    }
    public enum NotePriority
    {
        VeryHigh,
        High,
        Meduim,
        Low,
        VeryLow
    }
    public class Note
    {
        public bool Done;
        public string Text = "";
        public List<SubNote> SubNotes = new List<SubNote>();
        public NotePriority Prio;
    }
    public class SubNote
    {
        public bool Done;
        public string Text = "";
    }
}
