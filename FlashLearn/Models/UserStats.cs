namespace FlashLearn.Models;

public class UserStats
{
    public int TotalPoints { get; set; }
    public int CurrentStreak { get; set; }
    public int PersonalBestStreak { get; set; }
    public int MasteredCards { get; set; }
    public int TotalCards { get; set; }
    public DateTime LastStudyDate { get; set; } = DateTime.MinValue;
}
