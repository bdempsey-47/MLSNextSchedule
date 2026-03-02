namespace YSS.Data.Entities;

public class RawIngestionLog
{
    public int Id { get; set; }
    public DateTime FetchedAt { get; set; }
    public int PageNumber { get; set; }
    public string RawHtml { get; set; } = null!;
    public int ParsedMatchCount { get; set; }
}
