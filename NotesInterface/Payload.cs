using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notes.Interface
{
    public class Payload
    {
        public DateTime SaveTime { get; set; }

        public string Source { get; set; }
        public long Checksum { get; set; }

        public List<Note> Notes { get; set; }

        public Payload()
        {
            SaveTime = new DateTime(2000, 1, 1);
            Notes = [];
            Source = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

            Checksum = GenerateChecksum();
        }

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
        public static Payload? Parse(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<Payload>(json);
            }
            catch
            {
                Logger.WriteLine($"Error parsing payload {json}", LogLevel.Error);
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
        public bool Done { get; set; } = false;
        public string Text { get; set; } = "";
        public bool Expanded { get; set; } = false;
        public List<Note> SubNotes { get; set; } = new();
        public NotePriority Prio { get; set; } = NotePriority.Meduim;
    }
}
