using System.Web;
using Newtonsoft.Json;

namespace Notes.Interface.DTO;

public class NoteData
{
    public bool Done { get; set; } = false;
    public string Text { get; set; } = ""; // Should be URL encoded so that the json parser is not interrupted by special characters 
    [JsonIgnore]
    public string DecodedText
    {
        get
        {
            return HttpUtility.UrlDecode(Text) ?? "";
        }
        set { Text = HttpUtility.UrlEncode(value); }
    } // Should be URL encoded so that the json parser is not interrupted by special characters 
    public bool Expanded { get; set; } = false;
    public bool Hidden { get; set; } = false;
    public NotePriority Prio { get; set; } = NotePriority.Medium;
}