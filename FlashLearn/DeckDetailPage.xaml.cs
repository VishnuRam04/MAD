using FlashLearn.Models;
using FlashLearn.Services;

namespace FlashLearn;

[QueryProperty(nameof(Deck), "Deck")]
public partial class DeckDetailPage : ContentPage
{
    private Deck _deck = new();
    private List<FlashCard> _cards = new();

    public Deck Deck
    {
        get => _deck;
        set
        {
            _deck = value;
            SetupHeader();
        }
    }

    public DeckDetailPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCards();
    }

    private void SetupHeader()
    {
        NavTitleLabel.Text = _deck.Title;
        DeckTitleLabel.Text = _deck.Title;
        DeckDescLabel.Text = $"Study your {_deck.Category.ToLower()} flashcards";
        ModalSubtitle.Text = $"Adding to '{_deck.Title}'";
        UpdateMeta();
    }

    private void UpdateMeta()
    {
        DeckMetaLabel.Text = $"{_deck.Category.ToUpper()} \u2022 {_cards.Count} Cards";
    }

    private async Task LoadCards()
    {
        try
        {
            _cards = await FirestoreService.Instance.GetCardsAsync(_deck.Id);
            UpdateMeta();
            RenderCards();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading cards: {ex.Message}");
        }
    }

    private void RenderCards()
    {
        CardsContainer.Children.Clear();

        if (_cards.Count == 0)
        {
            CardsContainer.Children.Add(new Label
            {
                Text = "No cards yet. Tap '+ Add Card' to create your first flashcard!",
                FontSize = 15,
                TextColor = Color.FromArgb("#596063"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40)
            });
            return;
        }

        for (int i = 0; i < _cards.Count; i++)
        {
            CardsContainer.Children.Add(CreateCardView(_cards[i], i + 1));
        }
    }

    private View CreateCardView(FlashCard card, int index)
    {
        var cardBorder = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 24 },
            StrokeThickness = 0,
            BackgroundColor = Colors.White,
            Padding = new Thickness(24),
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromArgb("#2D3337")),
                Offset = new Point(0, 8),
                Radius = 24,
                Opacity = 0.04f
            }
        };

        var mainStack = new VerticalStackLayout { Spacing = 16 };

        var questionSection = new VerticalStackLayout { Spacing = 8 };

        var questionBadge = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            Padding = new Thickness(12, 5),
            HorizontalOptions = LayoutOptions.Start
        };
        questionBadge.Content = new Label
        {
            Text = "QUESTION",
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#0060AD"),
            CharacterSpacing = 1.5
        };
        questionSection.Children.Add(questionBadge);

        questionSection.Children.Add(new Label
        {
            Text = card.Question,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2D3337"),
            LineHeight = 1.4
        });

        mainStack.Children.Add(questionSection);

        mainStack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = Color.FromArgb("#EAEEF1"),
            HorizontalOptions = LayoutOptions.Fill
        });

        var answerSection = new VerticalStackLayout { Spacing = 8 };

        var answerBadge = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#F0FDF4"),
            Padding = new Thickness(12, 5),
            HorizontalOptions = LayoutOptions.Start
        };
        answerBadge.Content = new Label
        {
            Text = "ANSWER",
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#136E27"),
            CharacterSpacing = 1.5
        };
        answerSection.Children.Add(answerBadge);

        answerSection.Children.Add(new Label
        {
            Text = card.Answer,
            FontSize = 15,
            TextColor = Color.FromArgb("#596063"),
            LineHeight = 1.5
        });

        mainStack.Children.Add(answerSection);

        var actionsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            Margin = new Thickness(0, 4, 0, 0)
        };

        var actionDivider = new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = Color.FromArgb("#EAEEF1"),
            HorizontalOptions = LayoutOptions.Fill
        };
        mainStack.Children.Add(actionDivider);

        var editBtn = new Button
        {
            Text = "\u270E",
            FontSize = 18,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#596063"),
            Padding = new Thickness(8),
            WidthRequest = 44,
            HeightRequest = 44
        };
        editBtn.Clicked += async (s, e) => await OnEditCard(card);
        Grid.SetColumn(editBtn, 1);
        actionsGrid.Children.Add(editBtn);

        var deleteBtn = new Button
        {
            Text = "\uD83D\uDDD1",
            FontSize = 18,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#AC3434"),
            Padding = new Thickness(8),
            WidthRequest = 44,
            HeightRequest = 44
        };
        deleteBtn.Clicked += async (s, e) => await OnDeleteCard(card);
        Grid.SetColumn(deleteBtn, 2);
        actionsGrid.Children.Add(deleteBtn);

        mainStack.Children.Add(actionsGrid);

        cardBorder.Content = mainStack;
        return cardBorder;
    }

    private async Task OnEditCard(FlashCard card)
    {
        var newQuestion = await DisplayPromptAsync("Edit Question", "Update the question:", initialValue: card.Question);
        if (newQuestion == null) return;

        var newAnswer = await DisplayPromptAsync("Edit Answer", "Update the answer:", initialValue: card.Answer);
        if (newAnswer == null) return;

        card.Question = newQuestion;
        card.Answer = newAnswer;

        var firestore = FirestoreService.Instance;
        await UpdateCardInFirestore(card);
        await LoadCards();
    }

    private async Task UpdateCardInFirestore(FlashCard card)
    {
        try
        {
            var httpClient = new HttpClient();
            var auth = FirebaseAuthService.Instance;
            var userId = auth.UserId ?? "default_user";
            var url = $"https://firestore.googleapis.com/v1/projects/{FirebaseConfig.ProjectId}/databases/(default)/documents/users/{userId}/decks/{card.DeckId}/cards/{card.Id}";

            var doc = new
            {
                fields = new Dictionary<string, object>
                {
                    ["deckId"] = new { stringValue = card.DeckId },
                    ["question"] = new { stringValue = card.Question },
                    ["answer"] = new { stringValue = card.Answer },
                    ["isAnswered"] = new { booleanValue = card.IsAnswered },
                    ["createdAt"] = new { timestampValue = card.CreatedAt.ToString("o") }
                }
            };
            var json = System.Text.Json.JsonSerializer.Serialize(doc);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            await httpClient.PatchAsync(url, content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating card: {ex.Message}");
        }
    }

    private async Task OnDeleteCard(FlashCard card)
    {
        var confirm = await DisplayAlert("Delete Card", "Are you sure you want to delete this card?", "Delete", "Cancel");
        if (!confirm) return;

        try
        {
            var httpClient = new HttpClient();
            var userId = FirebaseAuthService.Instance.UserId ?? "default_user";
            var url = $"https://firestore.googleapis.com/v1/projects/{FirebaseConfig.ProjectId}/databases/(default)/documents/users/{userId}/decks/{card.DeckId}/cards/{card.Id}";
            await httpClient.DeleteAsync(url);

            // Update deck card count
            _deck.TotalCards = Math.Max(0, _deck.TotalCards - 1);
            if (card.IsAnswered)
                _deck.AnsweredCards = Math.Max(0, _deck.AnsweredCards - 1);
            await FirestoreService.Instance.UpdateDeckAsync(_deck);

            await LoadCards();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting card: {ex.Message}");
        }
    }

    private async void OnStudyClicked(object sender, EventArgs e)
    {
        if (_deck.TotalCards == 0)
        {
            await DisplayAlert("No Cards", "Add some cards to this deck before studying.", "OK");
            return;
        }
        await Shell.Current.GoToAsync(nameof(StudySessionPage), new Dictionary<string, object>
        {
            ["Deck"] = _deck
        });
    }

    private void OnAddCardClicked(object sender, EventArgs e)
    {
        QuestionEditor.Text = string.Empty;
        AnswerEditor.Text = string.Empty;
        ModalOverlay.IsVisible = true;
    }

    private void OnModalCancelTapped(object sender, TappedEventArgs e)
    {
        ModalOverlay.IsVisible = false;
    }

    private async void OnSubmitCardClicked(object sender, EventArgs e)
    {
        var question = QuestionEditor.Text?.Trim();
        var answer = AnswerEditor.Text?.Trim();

        if (string.IsNullOrEmpty(question) || string.IsNullOrEmpty(answer))
        {
            await DisplayAlert("Missing Fields", "Please enter both a question and an answer.", "OK");
            return;
        }

        SubmitCardBtn.IsEnabled = false;

        try
        {
            var card = new FlashCard
            {
                DeckId = _deck.Id,
                Question = question,
                Answer = answer,
                IsAnswered = false,
                CreatedAt = DateTime.UtcNow
            };

            await FirestoreService.Instance.CreateCardAsync(card);

            _deck.TotalCards += 1;
            await FirestoreService.Instance.UpdateDeckAsync(_deck);

            ModalOverlay.IsVisible = false;
            await LoadCards();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to add card: {ex.Message}", "OK");
        }
        finally
        {
            SubmitCardBtn.IsEnabled = true;
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
