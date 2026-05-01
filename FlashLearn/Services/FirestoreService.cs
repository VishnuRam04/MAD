using System.Globalization;
using System.Text;
using System.Text.Json;
using FlashLearn.Models;

namespace FlashLearn.Services;

public class FirestoreService
{
    private static FirestoreService? _instance;
    public static FirestoreService Instance => _instance ??= new FirestoreService();

    private readonly HttpClient _httpClient = new();
    private string BaseUrl => $"https://firestore.googleapis.com/v1/projects/{FirebaseConfig.ProjectId}/databases/(default)/documents";

    private FirebaseAuthService Auth => FirebaseAuthService.Instance;
    private string CurrentUserId => Auth.UserId ?? "default_user";

    private async Task AddAuthHeaderAsync()
    {
        await Auth.EnsureValidTokenAsync();
        _httpClient.DefaultRequestHeaders.Clear();
        if (!string.IsNullOrEmpty(Auth.IdToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Auth.IdToken);
        }
    }


    public async Task SaveUserProfileAsync(UserProfile profile)
    {
        await AddAuthHeaderAsync();
        var url = $"{BaseUrl}/users/{profile.Uid}";
        var doc = new
        {
            fields = new Dictionary<string, object>
            {
                ["uid"] = new { stringValue = profile.Uid },
                ["email"] = new { stringValue = profile.Email },
                ["displayName"] = new { stringValue = profile.DisplayName },
                ["createdAt"] = new { timestampValue = profile.CreatedAt.ToString("o") }
            }
        };
        var json = JsonSerializer.Serialize(doc);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PatchAsync(url, content);
    }

    public async Task<UserProfile?> GetUserProfileAsync(string uid)
    {
        await AddAuthHeaderAsync();
        var url = $"{BaseUrl}/users/{uid}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            var fields = doc.RootElement.GetProperty("fields");
            return new UserProfile
            {
                Uid = GetStringField(fields, "uid"),
                Email = GetStringField(fields, "email"),
                DisplayName = GetStringField(fields, "displayName"),
                CreatedAt = GetTimestampField(fields, "createdAt")
            };
        }
        catch { return null; }
    }


    public async Task<string> CreateDeckAsync(Deck deck)
    {
        await AddAuthHeaderAsync();
        var url = $"{BaseUrl}/users/{CurrentUserId}/decks";
        var doc = new
        {
            fields = new Dictionary<string, object>
            {
                ["title"] = new { stringValue = deck.Title },
                ["category"] = new { stringValue = deck.Category },
                ["icon"] = new { stringValue = deck.Icon },
                ["totalCards"] = new { integerValue = deck.TotalCards.ToString() },
                ["answeredCards"] = new { integerValue = deck.AnsweredCards.ToString() },
                ["userId"] = new { stringValue = Auth.UserId ?? "" },
                ["createdAt"] = new { timestampValue = deck.CreatedAt.ToString("o") }
            }
        };
        var json = JsonSerializer.Serialize(doc);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Firestore error {response.StatusCode}: {body}");

        var result = JsonDocument.Parse(body);
        var name = result.RootElement.GetProperty("name").GetString() ?? "";
        return name.Split('/').Last();
    }

    public async Task<List<Deck>> GetDecksAsync()
    {
        await AddAuthHeaderAsync();
        var url = $"{BaseUrl}/users/{CurrentUserId}/decks";
        var decks = new List<Deck>();

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return decks;

            var body = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("documents", out var documents))
                return decks;

            foreach (var docElement in documents.EnumerateArray())
            {
                var fields = docElement.GetProperty("fields");
                var name = docElement.GetProperty("name").GetString() ?? "";
                var id = name.Split('/').Last();

                decks.Add(new Deck
                {
                    Id = id,
                    Title = GetStringField(fields, "title"),
                    Category = GetStringField(fields, "category"),
                    Icon = GetStringField(fields, "icon"),
                    TotalCards = GetIntField(fields, "totalCards"),
                    AnsweredCards = GetIntField(fields, "answeredCards"),
                    UserId = GetStringField(fields, "userId"),
                    CreatedAt = GetTimestampField(fields, "createdAt")
                });
            }
        }
        catch { }

        return decks;
    }

    public async Task UpdateDeckAsync(Deck deck)
    {
        await AddAuthHeaderAsync();
        var url = $"{BaseUrl}/users/{CurrentUserId}/decks/{deck.Id}";
        var doc = new
        {
            fields = new Dictionary<string, object>
            {
                ["title"] = new { stringValue = deck.Title },
                ["category"] = new { stringValue = deck.Category },
                ["icon"] = new { stringValue = deck.Icon },
                ["totalCards"] = new { integerValue = deck.TotalCards.ToString() },
                ["answeredCards"] = new { integerValue = deck.AnsweredCards.ToString() },
                ["userId"] = new { stringValue = deck.UserId },
                ["createdAt"] = new { timestampValue = deck.CreatedAt.ToString("o") }
            }
        };
        var json = JsonSerializer.Serialize(doc);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PatchAsync(url, content);
    }

    public async Task DeleteDeckAsync(string deckId)
    {
        await AddAuthHeaderAsync();
        var url = $"{BaseUrl}/users/{CurrentUserId}/decks/{deckId}";
        await _httpClient.DeleteAsync(url);
    }

    // ===== FLASH CARDS =====

    public async Task<string> CreateCardAsync(FlashCard card)
    {
        await AddAuthHeaderAsync();
        var url = $"{BaseUrl}/users/{CurrentUserId}/decks/{card.DeckId}/cards";
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
        var json = JsonSerializer.Serialize(doc);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content);
        var body = await response.Content.ReadAsStringAsync();
        var result = JsonDocument.Parse(body);
        var name = result.RootElement.GetProperty("name").GetString() ?? "";
        return name.Split('/').Last();
    }

    public async Task<List<FlashCard>> GetCardsAsync(string deckId)
    {
        await AddAuthHeaderAsync();
        var url = $"{BaseUrl}/users/{CurrentUserId}/decks/{deckId}/cards";
        var cards = new List<FlashCard>();

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return cards;

            var body = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("documents", out var documents))
                return cards;

            foreach (var docElement in documents.EnumerateArray())
            {
                var fields = docElement.GetProperty("fields");
                var name = docElement.GetProperty("name").GetString() ?? "";
                var id = name.Split('/').Last();

                cards.Add(new FlashCard
                {
                    Id = id,
                    DeckId = GetStringField(fields, "deckId"),
                    Question = GetStringField(fields, "question"),
                    Answer = GetStringField(fields, "answer"),
                    IsAnswered = GetBoolField(fields, "isAnswered"),
                    ReviewCount = GetIntField(fields, "reviewCount"),
                    LastReviewedAt = GetTimestampField(fields, "lastReviewedAt"),
                    NextReviewDate = GetTimestampField(fields, "nextReviewDate"),
                    CreatedAt = GetTimestampField(fields, "createdAt"),
                    EaseFactor = GetDoubleField(fields, "easeFactor", 2.5),
                    IntervalDays = GetIntField(fields, "intervalDays"),
                    Repetition = GetIntField(fields, "repetition")
                });
            }
        }
        catch { }

        return cards;
    }

    public async Task UpdateCardReviewAsync(FlashCard card)
    {
        await AddAuthHeaderAsync();
        var url = $"{BaseUrl}/users/{CurrentUserId}/decks/{card.DeckId}/cards/{card.Id}";
        var doc = new
        {
            fields = new Dictionary<string, object>
            {
                ["deckId"] = new { stringValue = card.DeckId },
                ["question"] = new { stringValue = card.Question },
                ["answer"] = new { stringValue = card.Answer },
                ["isAnswered"] = new { booleanValue = card.IsAnswered },
                ["reviewCount"] = new { integerValue = card.ReviewCount.ToString() },
                ["lastReviewedAt"] = new { timestampValue = card.LastReviewedAt.ToString("o") },
                ["nextReviewDate"] = new { timestampValue = card.NextReviewDate.ToString("o") },
                ["createdAt"] = new { timestampValue = card.CreatedAt.ToString("o") },
                ["easeFactor"] = new { doubleValue = card.EaseFactor },
                ["intervalDays"] = new { integerValue = card.IntervalDays.ToString() },
                ["repetition"] = new { integerValue = card.Repetition.ToString() }
            }
        };
        var json = JsonSerializer.Serialize(doc);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PatchAsync(url, content);
    }


    public async Task<UserStats> GetUserStatsAsync()
    {
        await AddAuthHeaderAsync();
        var url = $"{BaseUrl}/users/{CurrentUserId}/stats/summary";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new UserStats();
            var body = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            var fields = doc.RootElement.GetProperty("fields");
            return new UserStats
            {
                TotalPoints = GetIntField(fields, "totalPoints"),
                CurrentStreak = GetIntField(fields, "currentStreak"),
                PersonalBestStreak = GetIntField(fields, "personalBestStreak"),
                MasteredCards = GetIntField(fields, "masteredCards"),
                TotalCards = GetIntField(fields, "totalCards"),
                LastStudyDate = GetTimestampField(fields, "lastStudyDate")
            };
        }
        catch { return new UserStats(); }
    }

    public async Task SaveUserStatsAsync(UserStats stats)
    {
        await AddAuthHeaderAsync();
        var url = $"{BaseUrl}/users/{CurrentUserId}/stats/summary";
        var doc = new
        {
            fields = new Dictionary<string, object>
            {
                ["totalPoints"] = new { integerValue = stats.TotalPoints.ToString() },
                ["currentStreak"] = new { integerValue = stats.CurrentStreak.ToString() },
                ["personalBestStreak"] = new { integerValue = stats.PersonalBestStreak.ToString() },
                ["masteredCards"] = new { integerValue = stats.MasteredCards.ToString() },
                ["totalCards"] = new { integerValue = stats.TotalCards.ToString() },
                ["lastStudyDate"] = new { timestampValue = stats.LastStudyDate.ToString("o") }
            }
        };
        var json = JsonSerializer.Serialize(doc);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PatchAsync(url, content);
    }

    public async Task RecordStudyActivityAsync(int cardsReviewed, int pointsEarned)
    {
        await AddAuthHeaderAsync();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var url = $"{BaseUrl}/users/{CurrentUserId}/studyActivity/{today}";

        // Get existing entry to accumulate
        int existingCards = 0;
        int existingPoints = 0;
        try
        {
            var getResp = await _httpClient.GetAsync(url);
            if (getResp.IsSuccessStatusCode)
            {
                var body = await getResp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);
                var fields = doc.RootElement.GetProperty("fields");
                existingCards = GetIntField(fields, "cardsReviewed");
                existingPoints = GetIntField(fields, "pointsEarned");
            }
        }
        catch { }

        var patchDoc = new
        {
            fields = new Dictionary<string, object>
            {
                ["cardsReviewed"] = new { integerValue = (existingCards + cardsReviewed).ToString() },
                ["pointsEarned"] = new { integerValue = (existingPoints + pointsEarned).ToString() },
                ["date"] = new { stringValue = today }
            }
        };
        var json = JsonSerializer.Serialize(patchDoc);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PatchAsync(url, content);
    }

    public Task<Dictionary<string, int>> GetWeeklyActivityAsync() => GetActivityAsync(7);

    public Task<Dictionary<string, int>> GetMonthlyActivityAsync() => GetActivityAsync(30);

    private async Task<Dictionary<string, int>> GetActivityAsync(int days)
    {
        await AddAuthHeaderAsync();
        var result = new Dictionary<string, int>();
        for (int i = days - 1; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddDays(-i).ToString("yyyy-MM-dd");
            result[date] = 0;
        }

        try
        {
            var url = $"{BaseUrl}/users/{CurrentUserId}/studyActivity";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return result;

            var body = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("documents", out var documents)) return result;

            foreach (var docElement in documents.EnumerateArray())
            {
                var fields = docElement.GetProperty("fields");
                var dateStr = GetStringField(fields, "date");
                var cards = GetIntField(fields, "cardsReviewed");
                if (result.ContainsKey(dateStr))
                    result[dateStr] = cards;
            }
        }
        catch { }

        return result;
    }


    private static string GetStringField(JsonElement fields, string key)
    {
        if (fields.TryGetProperty(key, out var prop) && prop.TryGetProperty("stringValue", out var val))
            return val.GetString() ?? "";
        return "";
    }

    private static int GetIntField(JsonElement fields, string key)
    {
        if (fields.TryGetProperty(key, out var prop) && prop.TryGetProperty("integerValue", out var val))
            return int.TryParse(val.GetString(), out var result) ? result : 0;
        return 0;
    }

    private static bool GetBoolField(JsonElement fields, string key)
    {
        if (fields.TryGetProperty(key, out var prop) && prop.TryGetProperty("booleanValue", out var val))
            return val.GetBoolean();
        return false;
    }

    private static DateTime GetTimestampField(JsonElement fields, string key)
    {
        // Firestore returns RFC 3339 with a Z suffix. Force UTC on parse so callers
        // that compare to DateTime.UtcNow don't get bitten by an implicit local conversion.
        const DateTimeStyles styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;
        if (fields.TryGetProperty(key, out var prop) && prop.TryGetProperty("timestampValue", out var val))
            return DateTime.TryParse(val.GetString(), CultureInfo.InvariantCulture, styles, out var result)
                ? result
                : DateTime.UtcNow;
        return DateTime.UtcNow;
    }

    private static double GetDoubleField(JsonElement fields, string key, double defaultValue = 0.0)
    {
        if (!fields.TryGetProperty(key, out var prop)) return defaultValue;
        if (prop.TryGetProperty("doubleValue", out var dval) && dval.TryGetDouble(out var d)) return d;
        if (prop.TryGetProperty("integerValue", out var ival) && double.TryParse(ival.GetString(), out var i)) return i;
        return defaultValue;
    }
}
