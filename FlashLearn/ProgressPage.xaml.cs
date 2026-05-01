using FlashLearn.Models;
using FlashLearn.Services;

namespace FlashLearn;

public partial class ProgressPage : ContentPage
{
    private bool _showingWeek = true;
    private Dictionary<string, int> _weeklyActivity = new();
    private Dictionary<string, int> _monthlyActivity = new();
    private double _chartWidth = 300;
    private double _masteredPct = 0;

    public ProgressPage()
    {
        InitializeComponent();
    }

    // Same star-ratio Grid recipe the deck cards use (works because the column
    // definitions are baked in at construction time, not mutated post-layout).
    private void RenderMasteredBar()
    {
        var fillRatio = Math.Max(0.001, Math.Min(1.0, _masteredPct));
        var bar = new Grid
        {
            HeightRequest = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(fillRatio, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(Math.Max(0.001, 1 - fillRatio), GridUnitType.Star))
            }
        };

        var track = new BoxView
        {
            BackgroundColor = (Color)Application.Current!.Resources["Gray100"],
            CornerRadius = 5,
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = 10
        };
        Grid.SetColumnSpan(track, 2);
        bar.Children.Add(track);

        var fill = new BoxView
        {
            BackgroundColor = (Color)Application.Current!.Resources["Primary"],
            CornerRadius = 5,
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = 10
        };
        Grid.SetColumn(fill, 0);
        bar.Children.Add(fill);

        MasteredProgressHost.Children.Clear();
        MasteredProgressHost.Children.Add(bar);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateNavAvatar();
        await LoadData();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        _chartWidth = width - 72; // account for card padding + page padding
        if (_weeklyActivity.Count > 0 || _monthlyActivity.Count > 0)
            RenderActiveChart();
    }

    private void UpdateNavAvatar()
    {
        var name = FirebaseAuthService.Instance.DisplayName
                   ?? FirebaseAuthService.Instance.UserEmail
                   ?? "U";
        NavAvatar.Text = name.Length > 0 ? name[0].ToString().ToUpper() : "U";
        ProfilePhotoService.ApplyTo(NavAvatarImage, NavAvatar);
    }

    private async Task LoadData()
    {
        try
        {
            var statsTask = FirestoreService.Instance.GetUserStatsAsync();
            var decksTask = FirestoreService.Instance.GetDecksAsync();
            var weeklyTask = FirestoreService.Instance.GetWeeklyActivityAsync();
            var monthlyTask = FirestoreService.Instance.GetMonthlyActivityAsync();

            await Task.WhenAll(statsTask, decksTask, weeklyTask, monthlyTask);

            var stats = statsTask.Result;
            var decks = decksTask.Result;
            _weeklyActivity = weeklyTask.Result;
            _monthlyActivity = monthlyTask.Result;

            if (stats.TotalCards == 0 && decks.Count > 0)
            {
                stats.MasteredCards = decks.Sum(d => d.AnsweredCards);
                stats.TotalCards = decks.Sum(d => d.TotalCards);
            }

            UpdateStatsUI(stats);
            RenderActiveChart();
            RenderPerformanceBreakdown(decks);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ProgressPage load error: {ex.Message}");
        }
    }

    private void RenderActiveChart()
    {
        if (_showingWeek)
            RenderChart(_weeklyActivity);
        else
            RenderChart(BucketIntoWeeks(_monthlyActivity));
    }

    // Aggregate ~30 days into 4 weekly buckets so the chart layout stays readable.
    // Keys are W1..W4 (oldest → newest) so RenderChart's alphabetical sort places
    // the current week on the right.
    private static Dictionary<string, int> BucketIntoWeeks(Dictionary<string, int> daily)
    {
        var buckets = new Dictionary<string, int>();
        if (daily.Count == 0) return buckets;

        var ordered = daily.Where(kvp => DateTime.TryParse(kvp.Key, out _))
                           .Select(kvp => (date: DateTime.Parse(kvp.Key), count: kvp.Value))
                           .OrderBy(x => x.date)
                           .ToList();
        if (ordered.Count == 0) return buckets;

        // Anchor on today so the rightmost bucket is "this week".
        var end = DateTime.UtcNow.Date;
        for (int w = 0; w < 4; w++)
        {
            var bucketEnd = end.AddDays(-7 * (3 - w));
            var bucketStart = bucketEnd.AddDays(-6);
            int sum = ordered.Where(x => x.date.Date >= bucketStart && x.date.Date <= bucketEnd)
                             .Sum(x => x.count);
            buckets[$"W{w + 1}"] = sum;
        }
        return buckets;
    }

    private void UpdateStatsUI(UserStats stats)
    {
        TotalPointsLabel.Text = stats.TotalPoints.ToString("N0");
        StreakLabel.Text = stats.CurrentStreak.ToString();
        PersonalBestLabel.Text = $"Best: {stats.PersonalBestStreak} days";
        MasteredLabel.Text = stats.MasteredCards.ToString("N0");

        double masteredPct = stats.TotalCards > 0
            ? (double)stats.MasteredCards / stats.TotalCards
            : 0;

        int pct = (int)(masteredPct * 100);
        MasteredPercentLabel.Text = $"{pct}%";
        MasteredSubLabel.Text = "of your total collection";

        _masteredPct = masteredPct;
        RenderMasteredBar();

        PointsChangeLabel.Text = stats.TotalPoints > 0
            ? $"+{stats.TotalPoints:N0} total"
            : "+0 this week";
    }

    private void RenderChart(Dictionary<string, int> activity)
    {
        ChartContainer.Children.Clear();
        DayLabelsRow.Children.Clear();
        DayLabelsRow.ColumnDefinitions.Clear();

        var days = activity.OrderBy(kvp => kvp.Key).ToList();
        if (days.Count == 0) return;

        int maxVal = days.Max(d => d.Value);
        if (maxVal == 0) maxVal = 1;

        double chartHeight = 100.0;
        int peakIndex = days.Select((d, i) => (d, i))
                            .OrderByDescending(x => x.d.Value)
                            .First().i;

        var barsGrid = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        for (int col = 0; col < days.Count; col++)
            barsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        barsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));  // tooltip
        barsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));  // bars

        for (int i = 0; i < days.Count; i++)
        {
            var (date, count) = (days[i].Key, days[i].Value);
            double fillRatio = (double)count / maxVal;
            bool today = IsToday(date);

            var barStack = new Grid
            {
                VerticalOptions = LayoutOptions.Fill,
                Margin = new Thickness(4, 0)
            };

            barStack.Children.Add(new BoxView
            {
                BackgroundColor = Color.FromArgb("#EEF2FF"),
                CornerRadius = 6,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            });

            // Filled portion
            if (count > 0)
            {
                barStack.Children.Add(new BoxView
                {
                    BackgroundColor = today
                        ? Color.FromArgb("#1D4ED8")
                        : Color.FromArgb("#2563EB"),
                    CornerRadius = 6,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.End,
                    HeightRequest = fillRatio * chartHeight
                });
            }

            Grid.SetRow(barStack, 1);
            Grid.SetColumn(barStack, i);
            barsGrid.Children.Add(barStack);

            if (i == peakIndex && count > 0)
            {
                var bubble = new Border
                {
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                    StrokeThickness = 0,
                    BackgroundColor = Color.FromArgb("#1E293B"),
                    Padding = new Thickness(8, 4),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.End,
                    Margin = new Thickness(0, 0, 0, 6)
                };
                bubble.Content = new Label
                {
                    Text = $"{count}",
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White
                };
                Grid.SetRow(bubble, 0);
                Grid.SetColumn(bubble, i);
                barsGrid.Children.Add(bubble);
            }

            DayLabelsRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var dayLabel = new Label
            {
                Text = GetShortDayName(date),
                FontSize = 11,
                TextColor = today ? Color.FromArgb("#2563EB") : Color.FromArgb("#9CA3AF"),
                FontAttributes = today ? FontAttributes.Bold : FontAttributes.None,
                HorizontalTextAlignment = TextAlignment.Center
            };
            Grid.SetColumn(dayLabel, i);
            DayLabelsRow.Children.Add(dayLabel);
        }

        ChartContainer.Children.Add(barsGrid);
    }

    private void RenderPerformanceBreakdown(List<Deck> decks)
    {
        PerformanceContainer.Children.Clear();

        var ranked = decks.Where(d => d.TotalCards > 0)
                          .OrderByDescending(d => d.Progress)
                          .ToList();

        if (ranked.Count == 0)
        {
            PerformanceContainer.Children.Add(new Border
            {
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                StrokeThickness = 0,
                BackgroundColor = Colors.White,
                Padding = new Thickness(24, 32),
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    HorizontalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label { Text = "📚", FontSize = 32, HorizontalTextAlignment = TextAlignment.Center },
                        new Label
                        {
                            Text = "No deck data yet",
                            FontSize = 15,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#111827"),
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Label
                        {
                            Text = "Start studying to see your performance breakdown.",
                            FontSize = 13,
                            TextColor = Color.FromArgb("#6B7280"),
                            HorizontalTextAlignment = TextAlignment.Center,
                            LineHeight = 1.5
                        }
                    }
                }
            });
            return;
        }

        foreach (var deck in ranked)
            PerformanceContainer.Children.Add(CreatePerformanceCard(deck));
    }

    private static View CreatePerformanceCard(Deck deck)
    {
        int accuracy = (int)(deck.Progress * 100);
        string level = accuracy >= 90 ? "Expert" : accuracy >= 70 ? "Proficient" : "Learning";
        string accentHex = accuracy >= 90 ? "#16A34A" : accuracy >= 70 ? "#2563EB" : "#D97706";
        string bgHex = accuracy >= 90 ? "#DCFCE7" : accuracy >= 70 ? "#DBEAFE" : "#FEF3C7";
        var accentColor = Color.FromArgb(accentHex);
        var bgColor = Color.FromArgb(bgHex);

        var card = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            StrokeThickness = 0,
            BackgroundColor = Colors.White,
            Padding = new Thickness(16)
        };
        card.Shadow = new Shadow
        {
            Brush = new SolidColorBrush(Color.FromArgb("#00000018")),
            Offset = new Point(0, 2),
            Radius = 12,
            Opacity = 0.07f
        };

        var root = new VerticalStackLayout { Spacing = 12 };

        var topRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var iconBorder = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            StrokeThickness = 0,
            BackgroundColor = bgColor,
            WidthRequest = 46,
            HeightRequest = 46,
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = string.IsNullOrEmpty(deck.Icon) ? "📚" : deck.Icon,
                FontSize = 20,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        Grid.SetColumn(iconBorder, 0);
        topRow.Children.Add(iconBorder);

        var nameStack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Spacing = 2,
            Children =
            {
                new Label
                {
                    Text = deck.Title,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#111827"),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                },
                new Label
                {
                    Text = $"{deck.AnsweredCards:N0} cards reviewed",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#9CA3AF")
                }
            }
        };
        Grid.SetColumn(nameStack, 1);
        topRow.Children.Add(nameStack);

        var badge = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            StrokeThickness = 0,
            BackgroundColor = bgColor,
            Padding = new Thickness(10, 6),
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = $"{accuracy}%",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = accentColor
            }
        };
        Grid.SetColumn(badge, 2);
        topRow.Children.Add(badge);

        root.Children.Add(topRow);

        var progressTrack = new Grid
        {
            HeightRequest = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(deck.Progress, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(Math.Max(0.001, 1 - deck.Progress), GridUnitType.Star))
            }
        };
        var trackBg = new BoxView { BackgroundColor = Color.FromArgb("#F3F4F6"), CornerRadius = 4, HorizontalOptions = LayoutOptions.Fill };
        Grid.SetColumnSpan(trackBg, 2);
        progressTrack.Children.Add(trackBg);
        var trackFill = new BoxView { BackgroundColor = accentColor, CornerRadius = 4, HorizontalOptions = LayoutOptions.Fill };
        Grid.SetColumn(trackFill, 0);
        progressTrack.Children.Add(trackFill);

        var bottomRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        bottomRow.Children.Add(progressTrack);
        Grid.SetColumn(progressTrack, 0);

        var levelBadge = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 0,
            BackgroundColor = bgColor,
            Padding = new Thickness(8, 3),
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(10, 0, 0, 0),
            Content = new Label
            {
                Text = level,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = accentColor
            }
        };
        Grid.SetColumn(levelBadge, 1);
        bottomRow.Children.Add(levelBadge);

        root.Children.Add(bottomRow);
        card.Content = root;
        return card;
    }

    private void OnWeekToggle(object sender, TappedEventArgs e)
    {
        if (_showingWeek) return;
        _showingWeek = true;
        SetToggle(WeekToggle, true);
        SetToggle(MonthToggle, false);
        RenderActiveChart();
    }

    private void OnMonthToggle(object sender, TappedEventArgs e)
    {
        if (!_showingWeek) return;
        _showingWeek = false;
        SetToggle(MonthToggle, true);
        SetToggle(WeekToggle, false);
        RenderActiveChart();
    }

    private static void SetToggle(Border toggle, bool active)
    {
        toggle.BackgroundColor = active
            ? Color.FromArgb("#2563EB")
            : Colors.Transparent;

        if (toggle.Content is Label lbl)
        {
            lbl.TextColor = active ? Colors.White : Color.FromArgb("#6B7280");
            lbl.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    private static string GetShortDayName(string dateStr)
    {
        if (DateTime.TryParse(dateStr, out var dt))
            return dt.ToString("ddd").ToUpper()[..3];
        return dateStr.Length >= 3 ? dateStr[..3].ToUpper() : dateStr.ToUpper();
    }

    private static bool IsToday(string dateStr)
    {
        if (DateTime.TryParse(dateStr, out var dt))
            return dt.Date == DateTime.UtcNow.Date;
        // Monthly view uses synthetic W1..W4 keys; W4 is "this week".
        return dateStr == "W4";
    }
}
