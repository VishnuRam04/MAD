namespace FlashLearn.Models;

public class Deck
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int TotalCards { get; set; }
    public int AnsweredCards { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public double Progress => TotalCards > 0 ? (double)AnsweredCards / TotalCards : 0;
}
