using System.Text;
using System.Text.Json;

namespace Notes.Interface.DTO;

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
        Encoding.Unicode.GetBytes(Notes.Select(x => x.Data.Text).Combine("")).Select(x => (int)x).Sum();

    public List<NotePosition> GetAllNotes() => Notes.SelectMany(x => x.RecursiveSubNotes(depth: 1)).ToList();

    public override string ToString()
    {
        // Note text is URL-encoded, so values never contain raw quotes/newlines that would need
        // escaping - plain JSON serialization is safe here (the old Newtonsoft output used escape
        // stripping hacks for the same reason).
        return JsonSerializer.Serialize(this, NoteJson.Default);
    }
    public static Payload? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Payload>(json, NoteJson.Default);
        }
        catch
        {
            Logger.WriteLine($"Error parsing payload {json}", LogLevel.Error);
            return null;
        }
    }
}
