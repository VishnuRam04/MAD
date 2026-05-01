using FlashLearn.Models;
using FlashLearn.Services;

namespace FlashLearn;

public partial class MainPage : ContentPage
{
    private List<Deck> _decks = new();

    private static readonly (string bg, string icon, string accent)[] _palettes =
    {
        ("#DBEAFE", "#2563EB22", "#2563EB"),
        ("#EDE9FE", "#7C3AED22", "#7C3AED"),
        ("#DCFCE7", "#16A34A22", "#16A34A"),
        ("#FEF3C7", "#D9770622", "#D97706"),
        ("#FCE7F3", "#DB277722", "#DB2777"),
        ("#CFFAFE", "#0891B222", "#0891B2"),
    };

    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        SetGreeting();
        UpdateNavAvatar();
        await LoadDecksFromFirestore();
    }

    private void UpdateNavAvatar()
    {
        var name = FirebaseAuthService.Instance.DisplayName
                   ?? FirebaseAuthService.Instance.UserEmail
                   ?? "U";
        NavAvatar.Text = name.Length > 0 ? name[0].ToString().ToUpper() : "U";
        ProfilePhotoService.ApplyTo(NavAvatarImage, NavAvatar);
    }

    private void SetGreeting()
    {
        var hour = DateTime.Now.Hour;
        var greeting = hour switch
        {
            < 12 => "Good Morning 👋",
            < 17 => "Good Afternoon 👋",
            _    => "Good Evening 👋"
        };
        GreetingLabel.Text = greeting;
    }

    private async Task LoadDecksFromFirestore()
    {
        try
        {
            _decks = await FirestoreService.Instance.GetDecksAsync();
            RenderDecks();
            UpdateStats();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading decks: {ex.Message}");
        }
    }

    private void UpdateStats()
    {
        TotalDecksLabel.Text = _decks.Count.ToString();
        var totalAnswered = _decks.Sum(d => d.AnsweredCards);
        AnsweredCardsLabel.Text = $"{totalAnswered} cards answered";
    }

    private void RenderDecks()
    {
        DecksContainer.Children.Clear();

        if (_decks.Count == 0)
        {
            EmptyStateCard.IsVisible = true;
            return;
        }

        EmptyStateCard.IsVisible = false;

        for (int i = 0; i < _decks.Count; i++)
        {
            DecksContainer.Children.Add(CreateDeckCard(_decks[i], i));
        }
    }

    // Same tiering used by ProgressPage's performance breakdown so the bar colors stay consistent.
    private static (string accent, string bg) ProgressTier(double progress)
    {
        int pct = (int)(progress * 100);
        if (pct >= 90) return ("#16A34A", "#DCFCE7");
        if (pct >= 70) return ("#2563EB", "#DBEAFE");
        return ("#D97706", "#FEF3C7");
    }

    private View CreateDeckCard(Deck deck, int index)
    {
        var palette = _palettes[index % _palettes.Length];
        var tier = ProgressTier(deck.Progress);

        var card = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
            StrokeThickness = 0,
            BackgroundColor = Colors.White,
            Padding = new Thickness(18, 16)
        };

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) => await OnDeckTapped(deck);
        card.GestureRecognizers.Add(tapGesture);

        var root = new VerticalStackLayout { Spacing = 12 };

        var topRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 14
        };

        var iconBorder = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb(palette.bg),
            WidthRequest = 52,
            HeightRequest = 52,
            VerticalOptions = LayoutOptions.Center
        };
        iconBorder.Content = new Label
        {
            Text = string.IsNullOrEmpty(deck.Icon) ? "📖" : deck.Icon,
            FontSize = 24,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(iconBorder, 0);
        topRow.Children.Add(iconBorder);

        var titleStack = new VerticalStackLayout
        {
            Spacing = 3,
            VerticalOptions = LayoutOptions.Center
        };
        titleStack.Children.Add(new Label
        {
            Text = deck.Title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111827"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        });
        titleStack.Children.Add(new Label
        {
            Text = $"{deck.TotalCards} Cards  •  {deck.Category}",
            FontSize = 12,
            TextColor = Color.FromArgb("#6B7280")
        });
        Grid.SetColumn(titleStack, 1);
        topRow.Children.Add(titleStack);

        var deleteBtn = new Button
        {
            Text = "🗑",
            FontSize = 16,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#D1D5DB"),
            Padding = new Thickness(0),
            WidthRequest = 36,
            HeightRequest = 36,
            VerticalOptions = LayoutOptions.Center
        };
        deleteBtn.Clicked += async (s, e) => await OnDeleteDeck(deck);
        Grid.SetColumn(deleteBtn, 2);
        topRow.Children.Add(deleteBtn);

        root.Children.Add(topRow);

        var progressRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 6
        };

        var progressLabel = new Label
        {
            Text = "PROGRESS",
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(tier.accent),
            CharacterSpacing = 1
        };
        Grid.SetRow(progressLabel, 0);
        Grid.SetColumn(progressLabel, 0);
        progressRow.Children.Add(progressLabel);

        var pct = (int)(deck.Progress * 100);
        var pctPill = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb(tier.bg),
            Padding = new Thickness(8, 2),
            VerticalOptions = LayoutOptions.Center
        };
        pctPill.Content = new Label
        {
            Text = $"{pct}%",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(tier.accent)
        };
        Grid.SetRow(pctPill, 0);
        Grid.SetColumn(pctPill, 1);
        progressRow.Children.Add(pctPill);

        var barBg = new BoxView
        {
            HeightRequest = 7,
            BackgroundColor = Color.FromArgb("#F3F4F6"),
            HorizontalOptions = LayoutOptions.Fill
        };
        barBg.Clip = new Microsoft.Maui.Controls.Shapes.RoundRectangleGeometry
        {
            CornerRadius = 4,
            Rect = new Rect(0, 0, 1000, 7)
        };

        var barFill = new BoxView
        {
            HeightRequest = 7,
            BackgroundColor = Color.FromArgb(tier.accent),
            HorizontalOptions = LayoutOptions.Fill,
        };
        barFill.Clip = new Microsoft.Maui.Controls.Shapes.RoundRectangleGeometry
        {
            CornerRadius = 4,
            Rect = new Rect(0, 0, 1000, 7)
        };

        var fillRatio = Math.Max(0.001, deck.Progress);
        var barGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(fillRatio, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(Math.Max(0.001, 1 - fillRatio), GridUnitType.Star))
            },
            HeightRequest = 7
        };
        Grid.SetColumnSpan(barBg, 2);
        barGrid.Children.Add(barBg);
        barGrid.Children.Add(barFill);
        Grid.SetColumn(barFill, 0);

        Grid.SetRow(barGrid, 1);
        Grid.SetColumnSpan(barGrid, 2);
        progressRow.Children.Add(barGrid);

        root.Children.Add(progressRow);

        card.Content = root;
        return card;
    }

    private async Task OnDeckTapped(Deck deck)
    {
        await Shell.Current.GoToAsync(nameof(DeckDetailPage), new Dictionary<string, object>
        {
            ["Deck"] = deck
        });
    }

    private async Task OnDeleteDeck(Deck deck)
    {
        var confirm = await DisplayAlert("Delete Deck", $"Are you sure you want to delete '{deck.Title}'?", "Delete", "Cancel");
        if (!confirm) return;

        await FirestoreService.Instance.DeleteDeckAsync(deck.Id);
        await LoadDecksFromFirestore();
    }

    private async void OnAddDeckClicked(object sender, EventArgs e)
    {
        var title = await DisplayPromptAsync("New Deck", "Enter deck title:");
        if (string.IsNullOrWhiteSpace(title)) return;

        var category = await DisplayPromptAsync("New Deck", "Enter category (e.g., Science, Language):");
        if (string.IsNullOrWhiteSpace(category)) return;

        try
        {
            var deck = new Deck
            {
                Title = title,
                Category = category,
                Icon = "📖",
                TotalCards = 0,
                AnsweredCards = 0,
                UserId = FirebaseAuthService.Instance.UserId ?? "",
                CreatedAt = DateTime.UtcNow
            };

            await FirestoreService.Instance.CreateDeckAsync(deck);
            await LoadDecksFromFirestore();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Add deck error: {ex.Message}");
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
