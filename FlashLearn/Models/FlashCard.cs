namespace FlashLearn.Models;

public class FlashCard
{
    public string Id { get; set; } = string.Empty;
    public string DeckId { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public bool IsAnswered { get; set; }
    public int ReviewCount { get; set; }
    public DateTime LastReviewedAt { get; set; } = DateTime.MinValue;
    public DateTime NextReviewDate { get; set; } = DateTime.MinValue;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // SM-2 spaced repetition state
    public double EaseFactor { get; set; } = 2.5;
    public int IntervalDays { get; set; } = 0;
    public int Repetition { get; set; } = 0;
}
