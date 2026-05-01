using FlashLearn.Services;

namespace FlashLearn;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Always start at login: clear any persisted auth state and route to LoginPage.
        FirebaseAuthService.Instance.Logout();
        MainPage = new LoginPage();
    }
}
