using System;
using System.Web;
using Newtonsoft.Json;

namespace Notes.Interface.DTO;

public class NoteData
{
    public bool Done { get; set; } = false;
    /// <summary>
    /// Canceled acts like Done in the UI (crossed through etc.). If both Done and Canceled are
    /// set, Done wins for display purposes; the UI never produces that combination itself.
    /// </summary>
    public bool Canceled { get; set; } = false;
    /// <summary>
    /// When the note was created. Null for notes created before this field existed; they are
    /// backfilled with the local "now" once when loaded (and synced via an update change).
    /// </summary>
    public DateTimeOffset? Created { get; set; }
    public string Text { get; set; } = ""; // Should be URL encoded so that the json parser is not interrupted by special characters 
    [JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
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