using PokerLiveScrapper.Interfaces;

namespace PokerLiveScrapper;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Force the app to always use light mode
		Application.Current!.UserAppTheme = AppTheme.Light;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		IAutomationService automationService = IPlatformApplication.Current
				!.Services.GetService<IAutomationService>()!;
		return new Window(new MainPage(automationService));
	}
}