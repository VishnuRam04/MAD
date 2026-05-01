using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace FlashLearn;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if IOS
		builder.ConfigureLifecycleEvents(events =>
		{
			events.AddiOS(ios => ios.FinishedLaunching((app, launchOptions) =>
			{
				var tabBarAppearance = new UIKit.UITabBarAppearance();
				tabBarAppearance.ConfigureWithOpaqueBackground();
				tabBarAppearance.BackgroundColor = UIKit.UIColor.White;
				UIKit.UITabBar.Appearance.StandardAppearance = tabBarAppearance;
				UIKit.UITabBar.Appearance.ScrollEdgeAppearance = tabBarAppearance;
				return true;
			}));
		});
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
