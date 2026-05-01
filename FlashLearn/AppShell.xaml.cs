namespace FlashLearn;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(DeckDetailPage), typeof(DeckDetailPage));
		Routing.RegisterRoute(nameof(StudySessionPage), typeof(StudySessionPage));
	}
}
