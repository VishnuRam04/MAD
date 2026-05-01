namespace FlashLearn.Services;

public static class ProfilePhotoService
{
    public const string PhotoPrefKey = "profile_photo_path";

    public static string? GetPhotoPath()
    {
        var path = Preferences.Default.Get(PhotoPrefKey, string.Empty);
        return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;
    }

    // Toggle a header avatar between an image (photo) and a fallback initial label.
    // Pages call this in OnAppearing so a freshly-picked photo on ProfilePage shows up here too.
    public static void ApplyTo(Image image, Label fallbackLabel)
    {
        var path = GetPhotoPath();
        if (path != null)
        {
            image.Source = ImageSource.FromFile(path);
            image.IsVisible = true;
            fallbackLabel.IsVisible = false;
        }
        else
        {
            image.IsVisible = false;
            fallbackLabel.IsVisible = true;
        }
    }
}
