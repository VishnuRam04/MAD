using FlashLearn.Services;

namespace FlashLearn;

public partial class ProfilePage : ContentPage
{
    public ProfilePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadUserData();
        LoadSavedPhoto();
        ProfilePhotoService.ApplyTo(AvatarHeaderImage, AvatarInitialHeader);
    }

    private void LoadUserData()
    {
        var auth = FirebaseAuthService.Instance;
        var name = auth.DisplayName ?? auth.UserEmail ?? "User";
        var email = auth.UserEmail ?? string.Empty;

        DisplayNameLabel.Text = name;
        DisplayNameEntry.Text = name;
        EmailEntry.Text = email;

        var initial = name.Length > 0 ? name[0].ToString().ToUpper() : "U";
        AvatarInitial.Text = initial;
        AvatarInitialHeader.Text = initial;

        UserRankLabel.Text = "Luminescent Scholar";
    }

    private void LoadSavedPhoto()
    {
        var path = ProfilePhotoService.GetPhotoPath();
        if (path != null)
            ShowPhoto(ImageSource.FromFile(path));
    }

    private async void OnPickPhotoTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Choose a profile photo"
            });

            if (photo == null) return;

            var destDir = FileSystem.AppDataDirectory;
            // Use a fresh filename so MAUI doesn't serve a cached ImageSource for the previous photo.
            var destPath = Path.Combine(destDir, $"profile_photo_{DateTime.UtcNow.Ticks}.jpg");

            using (var srcStream = await photo.OpenReadAsync())
            using (var destStream = File.OpenWrite(destPath))
            {
                await srcStream.CopyToAsync(destStream);
            }

            // Best-effort cleanup of the previous photo on disk.
            var oldPath = Preferences.Default.Get(ProfilePhotoService.PhotoPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(oldPath) && File.Exists(oldPath) && oldPath != destPath)
            {
                try { File.Delete(oldPath); } catch { }
            }

            Preferences.Default.Set(ProfilePhotoService.PhotoPrefKey, destPath);
            ShowPhoto(ImageSource.FromFile(destPath));
            ProfilePhotoService.ApplyTo(AvatarHeaderImage, AvatarInitialHeader);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Photo pick error: {ex.Message}");
            await DisplayAlert("Error", "Could not load photo. Please try again.", "OK");
        }
    }

    private void ShowPhoto(ImageSource source)
    {
        ProfileImage.Source = source;
        PhotoBorder.IsVisible = true;
        InitialsBorder.IsVisible = false;
    }

    private async void OnSaveChangesClicked(object sender, EventArgs e)
    {
        var newName = DisplayNameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            await DisplayAlert("Validation", "Display name cannot be empty.", "OK");
            return;
        }

        DisplayNameLabel.Text = newName;
        var initial = newName[0].ToString().ToUpper();
        AvatarInitial.Text = initial;
        AvatarInitialHeader.Text = initial;

        FirebaseAuthService.Instance.UpdateDisplayName(newName);
        await DisplayAlert("Saved", "Your profile has been updated.", "OK");
    }

    private async void OnAboutTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new AboutPage());
    }

    private async void OnPrivacySettingsTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("Privacy Settings", "Privacy settings coming soon.", "OK");
    }

    private async void OnSignOutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlert("Sign Out", "Are you sure you want to sign out?", "Sign Out", "Cancel");
        if (!confirm) return;

        FirebaseAuthService.Instance.Logout();
        Application.Current!.MainPage = new LoginPage();
    }
}
