using System.Text;
using System.Text.Json;

namespace FlashLearn.Services;

public class FirebaseAuthService
{
    private static FirebaseAuthService? _instance;
    public static FirebaseAuthService Instance => _instance ??= new FirebaseAuthService();

    private readonly HttpClient _httpClient = new();

    private const string PrefToken        = "auth_id_token";
    private const string PrefRefreshToken = "auth_refresh_token";
    private const string PrefUserId       = "auth_user_id";
    private const string PrefEmail        = "auth_email";
    private const string PrefDisplayName  = "auth_display_name";
    private const string PrefTokenExpiry  = "auth_token_expiry";

    public string? IdToken     { get; private set; }
    public string? UserId      { get; private set; }
    public string? UserEmail   { get; private set; }
    public string? DisplayName { get; private set; }
    private string? _refreshToken;
    private DateTime _tokenExpiry;

    public bool IsLoggedIn => !string.IsNullOrEmpty(IdToken) || !string.IsNullOrEmpty(_refreshToken);

    private FirebaseAuthService()
    {
        IdToken       = Preferences.Default.Get(PrefToken,        string.Empty);
        _refreshToken = Preferences.Default.Get(PrefRefreshToken, string.Empty);
        UserId        = Preferences.Default.Get(PrefUserId,       string.Empty);
        UserEmail     = Preferences.Default.Get(PrefEmail,        string.Empty);
        DisplayName   = Preferences.Default.Get(PrefDisplayName,  string.Empty);

        var expiryStr = Preferences.Default.Get(PrefTokenExpiry, string.Empty);
        _tokenExpiry  = DateTime.TryParse(expiryStr, out var dt) ? dt : DateTime.MinValue;

        if (string.IsNullOrEmpty(IdToken) && string.IsNullOrEmpty(_refreshToken))
            IdToken = _refreshToken = UserId = UserEmail = DisplayName = null;
    }

    public async Task EnsureValidTokenAsync()
    {
        if (_tokenExpiry != DateTime.MinValue && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5)) return;
        if (string.IsNullOrEmpty(_refreshToken)) return;

        try
        {
            var url = $"https://securetoken.googleapis.com/v1/token?key={FirebaseConfig.ApiKey}";
            var payload = new { grant_type = "refresh_token", refresh_token = _refreshToken };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return;

            var result = JsonDocument.Parse(body);
            var newToken   = result.RootElement.GetProperty("id_token").GetString()!;
            var newRefresh = result.RootElement.GetProperty("refresh_token").GetString()!;
            var expiresIn  = int.Parse(result.RootElement.GetProperty("expires_in").GetString()!);

            IdToken       = newToken;
            _refreshToken = newRefresh;
            _tokenExpiry  = DateTime.UtcNow.AddSeconds(expiresIn);

            Preferences.Default.Set(PrefToken,        newToken);
            Preferences.Default.Set(PrefRefreshToken, newRefresh);
            Preferences.Default.Set(PrefTokenExpiry,  _tokenExpiry.ToString("o"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Token refresh error: {ex.Message}");
        }
    }

    public async Task<(bool success, string error)> RegisterAsync(string email, string password, string displayName)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={FirebaseConfig.ApiKey}";
        var payload = new { email, password, returnSecureToken = true };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var msg = JsonDocument.Parse(body).RootElement
                    .GetProperty("error").GetProperty("message").GetString();
                return (false, msg ?? "Registration failed");
            }

            var result = JsonDocument.Parse(body);
            Persist(
                result.RootElement.GetProperty("idToken").GetString()!,
                result.RootElement.GetProperty("refreshToken").GetString()!,
                result.RootElement.GetProperty("localId").GetString()!,
                email,
                displayName,
                3600
            );

            await UpdateProfileAsync(displayName);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string error)> LoginAsync(string email, string password)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={FirebaseConfig.ApiKey}";
        var payload = new { email, password, returnSecureToken = true };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var msg = JsonDocument.Parse(body).RootElement
                    .GetProperty("error").GetProperty("message").GetString();
                return (false, msg ?? "Login failed");
            }

            var result = JsonDocument.Parse(body);
            var name = result.RootElement.TryGetProperty("displayName", out var dn)
                ? dn.GetString() ?? email : email;

            Persist(
                result.RootElement.GetProperty("idToken").GetString()!,
                result.RootElement.GetProperty("refreshToken").GetString()!,
                result.RootElement.GetProperty("localId").GetString()!,
                email,
                name,
                3600
            );

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public void Logout()
    {
        IdToken = UserId = UserEmail = DisplayName = _refreshToken = null;
        Preferences.Default.Remove(PrefToken);
        Preferences.Default.Remove(PrefRefreshToken);
        Preferences.Default.Remove(PrefUserId);
        Preferences.Default.Remove(PrefEmail);
        Preferences.Default.Remove(PrefDisplayName);
        Preferences.Default.Remove(PrefTokenExpiry);
    }

    public void UpdateDisplayName(string name)
    {
        DisplayName = name;
        Preferences.Default.Set(PrefDisplayName, name);
    }

    private void Persist(string token, string refreshToken, string userId, string email, string name, int expiresInSeconds)
    {
        IdToken       = token;
        _refreshToken = refreshToken;
        UserId        = userId;
        UserEmail     = email;
        DisplayName   = name;
        _tokenExpiry  = DateTime.UtcNow.AddSeconds(expiresInSeconds);

        Preferences.Default.Set(PrefToken,        token);
        Preferences.Default.Set(PrefRefreshToken, refreshToken);
        Preferences.Default.Set(PrefUserId,       userId);
        Preferences.Default.Set(PrefEmail,        email);
        Preferences.Default.Set(PrefDisplayName,  name);
        Preferences.Default.Set(PrefTokenExpiry,  _tokenExpiry.ToString("o"));
    }

    private async Task UpdateProfileAsync(string displayName)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={FirebaseConfig.ApiKey}";
        var payload = new { idToken = IdToken, displayName, returnSecureToken = false };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        await _httpClient.PostAsync(url, content);
    }
}
