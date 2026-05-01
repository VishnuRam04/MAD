using FlashLearn.Services;

namespace FlashLearn;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        HideError();
        var email = EmailEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError("Please enter your email and password");
            return;
        }

        SetLoading(true);
        var (success, error) = await FirebaseAuthService.Instance.LoginAsync(email, password);
        SetLoading(false);

        if (success)
        {
            Application.Current!.MainPage = new AppShell();
        }
        else
        {
            ShowError(FormatError(error));
        }
    }

    private async void OnRegisterTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new RegisterPage());
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
        LoginBtn.IsEnabled = !loading;
        LoginBtn.Opacity = loading ? 0.6 : 1.0;
    }

    private static string FormatError(string error) => error switch
    {
        "EMAIL_NOT_FOUND"          => "No account found with this email.",
        "INVALID_PASSWORD"         => "Incorrect password. Please try again.",
        "USER_DISABLED"            => "This account has been disabled.",
        "INVALID_LOGIN_CREDENTIALS"=> "Invalid email or password.",
        _                          => error
    };
}
