using FlashLearn.Services;
using FlashLearn.Models;

namespace FlashLearn;

public partial class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        InitializeComponent();
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        HideError();
        var name = NameEntry.Text?.Trim();
        var email = EmailEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrEmpty(name))   { ShowError("Please enter your full name.");           return; }
        if (string.IsNullOrEmpty(email))  { ShowError("Please enter your email address.");       return; }
        if (string.IsNullOrEmpty(password))        { ShowError("Please enter a password.");      return; }
        if (password.Length < 6)          { ShowError("Password must be at least 6 characters."); return; }

        SetLoading(true);

        var auth = FirebaseAuthService.Instance;
        var (success, error) = await auth.RegisterAsync(email, password, name);

        if (success)
        {
            // Persist full profile to Firestore so ProfilePage can read it
            var profile = new UserProfile
            {
                Uid = auth.UserId!,
                Email = email,
                DisplayName = name,
                CreatedAt = DateTime.UtcNow
            };
            await FirestoreService.Instance.SaveUserProfileAsync(profile);

            SetLoading(false);
            Application.Current!.MainPage = new AppShell();
        }
        else
        {
            SetLoading(false);
            ShowError(FormatError(error));
        }
    }

    private async void OnLoginTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorCard.IsVisible = true;
    }

    private void HideError() => ErrorCard.IsVisible = false;

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsRunning = loading;
        LoadingIndicator.IsVisible = loading;
        RegisterBtn.IsEnabled = !loading;
        RegisterBtn.Opacity = loading ? 0.6 : 1.0;
    }

    private static string FormatError(string error) => error switch
    {
        "EMAIL_EXISTS"  => "An account with this email already exists.",
        "WEAK_PASSWORD" => "Password must be at least 6 characters.",
        "INVALID_EMAIL" => "Please enter a valid email address.",
        _               => error
    };
}
