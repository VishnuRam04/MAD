using FlashLearn.Models;
using FlashLearn.Services;

namespace FlashLearn;

[QueryProperty(nameof(Deck), "Deck")]
public partial class StudySessionPage : ContentPage
{
    private Deck _deck = new();
    private List<FlashCard> _queue = new();
    private int _currentIndex = 0;
    private int _sessionPoints = 0;
    private int _cardsReviewed = 0;
    private double _progressBarMaxWidth = 300;

    // Tracks how many times each card has been requeued via Hard in this session.
    // Caps requeues so the session can't loop forever on a small deck.
    private const int MaxHardRequeuesPerCard = 2;
    private readonly Dictionary<string, int> _sessionHardCount = new();

    private static readonly (string emoji, string text)[] _motivations =
    {
        ("🔥", "You're on fire!"),
        ("💪", "Keep crushing it!"),
        ("🧠", "Brain gains!"),
        ("✨", "One step closer!"),
        ("🚀", "On a roll!"),
        ("🎯", "Stay focused!"),
        ("⚡", "You got this!"),
        ("🎉", "Nailed it!"),
        ("💡", "Great recall!"),
        ("👏", "Nice work!"),
    };

    public Deck Deck
    {
        get => _deck;
        set
        {
            _deck = value;
            DeckTitleLabel.Text = _deck.Title;
        }
    }

    public StudySessionPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCards();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        _progressBarMaxWidth = width - 40;
        UpdateProgressBar();
    }

    private async Task LoadCards()
    {
        try
        {
            var cards = await FirestoreService.Instance.GetCardsAsync(_deck.Id);
            var now = DateTime.UtcNow;

            // Only include cards that are due now (or have never been reviewed).
            // New cards have NextReviewDate == default (MinValue) so they pass.
            _queue = cards
                .Where(c => c.NextReviewDate <= now)
                .OrderBy(c => c.NextReviewDate)
                .ToList();

            _currentIndex = 0;
            _sessionPoints = 0;
            _cardsReviewed = 0;
            _sessionHardCount.Clear();
            PointsBadgeLabel.Text = "+0 Points";

            if (cards.Count == 0)
            {
                await DisplayAlert("No Cards", "This deck has no cards yet. Add some from the deck detail page.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            if (_queue.Count == 0)
            {
                var nextDue = cards.Min(c => c.NextReviewDate);
                var when = nextDue == DateTime.MinValue ? "soon" : nextDue.ToLocalTime().ToString("g");
                var studyAnyway = await DisplayAlert(
                    "All Caught Up",
                    $"No cards are due right now. Next review: {when}.",
                    "Study Anyway",
                    "Back");

                if (!studyAnyway)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                // Override: practice all cards regardless of due date.
                _queue = cards.OrderBy(c => c.NextReviewDate).ToList();
            }

            ShowCurrentCard();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StudySession load error: {ex.Message}");
        }
    }

    private void ShowCurrentCard()
    {
        if (_currentIndex >= _queue.Count)
        {
            ShowCompletion();
            return;
        }

        var card = _queue[_currentIndex];
        QuestionLabel.Text = card.Question;
        AnswerLabel.Text = card.Answer;
        AnswerSection.IsVisible = false;
        RevealButton.IsVisible = true;
        RatingPanel.IsVisible = false;

        CardCounterLabel.Text = $"Card {_currentIndex + 1} of {_queue.Count}";
        UpdateProgressBar();
    }

    private void UpdateProgressBar()
    {
        if (_queue.Count == 0) return;
        var progress = (double)_currentIndex / _queue.Count;
        ProgressBarFill.WidthRequest = progress * _progressBarMaxWidth;
    }

    private void OnRevealTapped(object sender, TappedEventArgs e)
    {
        AnswerSection.IsVisible = true;
        RevealButton.IsVisible = false;
        RatingPanel.IsVisible = true;
    }

    private async void OnEasyTapped(object sender, TappedEventArgs e)
    {
        var card = _queue[_currentIndex];
        ApplySm2Easy(card);
        card.IsAnswered = true;
        card.ReviewCount++;
        card.LastReviewedAt = DateTime.UtcNow;

        int earned = 10;
        _sessionPoints += earned;
        _cardsReviewed++;
        PointsBadgeLabel.Text = $"+{_sessionPoints} Points";

        // Fire animations and save in parallel — don't await save before animating
        _ = FirestoreService.Instance.UpdateCardReviewAsync(card);
        await Task.WhenAll(
            ShowPointsPopup(earned, isEasy: true),
            ShowMotivation()
        );

        _currentIndex++;
        ShowCurrentCard();
    }

    private async void OnHardTapped(object sender, TappedEventArgs e)
    {
        var card = _queue[_currentIndex];
        ApplySm2Hard(card);
        card.IsAnswered = false;
        card.ReviewCount++;
        card.LastReviewedAt = DateTime.UtcNow;

        int earned = 5;
        _sessionPoints += earned;
        _cardsReviewed++;
        PointsBadgeLabel.Text = $"+{_sessionPoints} Points";

        _ = FirestoreService.Instance.UpdateCardReviewAsync(card);
        await Task.WhenAll(
            ShowPointsPopup(earned, isEasy: false),
            ShowMotivation()
        );

        _sessionHardCount.TryGetValue(card.Id, out int hardCount);
        hardCount++;
        _sessionHardCount[card.Id] = hardCount;

        if (hardCount < MaxHardRequeuesPerCard && _queue.Count > 1)
        {
            // Requeue: drop from current slot, append to end. Next card slides into _currentIndex.
            _queue.RemoveAt(_currentIndex);
            _queue.Add(card);
        }
        else
        {
            // Cap reached (or only one card left) — advance past it so the session can complete.
            _currentIndex++;
        }

        ShowCurrentCard();
    }

    // SM-2: Easy ≈ quality 5. Grow interval; nudge ease up.
    private static void ApplySm2Easy(FlashCard card)
    {
        card.Repetition++;
        if (card.Repetition == 1)
            card.IntervalDays = 1;
        else if (card.Repetition == 2)
            card.IntervalDays = 6;
        else
            card.IntervalDays = Math.Max(1, (int)Math.Round(card.IntervalDays * card.EaseFactor));

        card.EaseFactor += 0.1;
        card.NextReviewDate = DateTime.UtcNow.AddDays(card.IntervalDays);
    }

    // SM-2: Hard ≈ lapse. Reset repetitions, see again tomorrow, drop ease.
    private static void ApplySm2Hard(FlashCard card)
    {
        card.Repetition = 0;
        card.IntervalDays = 1;
        card.EaseFactor = Math.Max(1.3, card.EaseFactor - 0.2);
        card.NextReviewDate = DateTime.UtcNow.AddDays(1);
    }

    private async Task ShowPointsPopup(int points, bool isEasy)
    {
        PointsPopupLabel.Text = isEasy ? $"+{points} ⭐" : $"+{points} 💪";
        PointsPopupLabel.TextColor = isEasy
            ? Color.FromArgb("#16A34A")
            : Color.FromArgb("#D97706");
        PointsPopupLabel.IsVisible = true;
        PointsPopupLabel.Opacity = 1;
        PointsPopupLabel.Scale = 0.6;
        PointsPopupLabel.TranslationY = 0;

        // Pop in
        await PointsPopupLabel.ScaleTo(1.4, 180, Easing.CubicOut);
        await PointsPopupLabel.ScaleTo(1.1, 100, Easing.CubicIn);

        // Float up and fade out
        await Task.WhenAll(
            PointsPopupLabel.TranslateTo(0, -90, 700, Easing.CubicOut),
            PointsPopupLabel.FadeTo(0, 700)
        );

        PointsPopupLabel.IsVisible = false;
        PointsPopupLabel.TranslationY = 0;
        PointsPopupLabel.Scale = 1;
    }

    private async Task ShowMotivation()
    {
        var (emoji, text) = _motivations[_cardsReviewed % _motivations.Length];
        MotivationEmoji.Text = emoji;
        MotivationLabel.Text = text;

        MotivationToast.IsVisible = true;
        MotivationToast.Opacity = 0;
        MotivationToast.TranslationY = 40;

        // Slide up + fade in
        await Task.WhenAll(
            MotivationToast.TranslateTo(0, 0, 280, Easing.CubicOut),
            MotivationToast.FadeTo(1, 280)
        );

        await Task.Delay(900);

        // Slide down + fade out
        await Task.WhenAll(
            MotivationToast.TranslateTo(0, 40, 280, Easing.CubicIn),
            MotivationToast.FadeTo(0, 280)
        );

        MotivationToast.IsVisible = false;
        MotivationToast.TranslationY = 0;
    }

    private void ShowCompletion()
    {
        CompletionSubtitle.Text = $"You reviewed {_cardsReviewed} cards\nand earned {_sessionPoints} points!";
        CompletionOverlay.IsVisible = true;
        _ = SaveSessionStats();
    }

    private async Task SaveSessionStats()
    {
        try
        {
            var stats = await FirestoreService.Instance.GetUserStatsAsync();

            var today = DateTime.UtcNow.Date;
            var lastStudy = stats.LastStudyDate.Date;
            if (lastStudy == today.AddDays(-1))
                stats.CurrentStreak++;
            else if (lastStudy < today.AddDays(-1))
                stats.CurrentStreak = 1;

            stats.PersonalBestStreak = Math.Max(stats.PersonalBestStreak, stats.CurrentStreak);
            stats.TotalPoints += _sessionPoints;
            stats.LastStudyDate = DateTime.UtcNow;

            var allCards = await FirestoreService.Instance.GetCardsAsync(_deck.Id);
            stats.MasteredCards += allCards.Count(c => c.IsAnswered);

            await FirestoreService.Instance.SaveUserStatsAsync(stats);
            await FirestoreService.Instance.RecordStudyActivityAsync(_cardsReviewed, _sessionPoints);

            _deck.AnsweredCards = allCards.Count(c => c.IsAnswered);
            await FirestoreService.Instance.UpdateDeckAsync(_deck);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveSessionStats error: {ex.Message}");
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
