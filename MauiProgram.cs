using PokerLiveScrapper.Interfaces;
using PokerLiveScrapper.Platforms.Android;
using Microsoft.Extensions.Logging;

namespace PokerLiveScrapper;

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

		// Register services
		builder.Services.AddSingleton<IAutomationService, AndroidAutomationService>();

		// Register pages 
		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
		builder.Logging.AddConsole();
#endif

		return builder.Build();
	}
}
