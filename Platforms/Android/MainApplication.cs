using Android.App;
using Android.Runtime;
using AndroidX.AppCompat.App;

namespace PokerLiveScrapper;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
		//force light mode
		if (Microsoft.Maui.Controls.Application.Current != null)
		{
			Microsoft.Maui.Controls.Application.Current.UserAppTheme = AppTheme.Light;
		}
		//disable night mode
		AppCompatDelegate.DefaultNightMode = AppCompatDelegate.ModeNightNo;
	}

	protected override MauiApp CreateMauiApp()
	{
		var mauiApp = MauiProgram.CreateMauiApp();
		// Remove background from the editor (Android)
		Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("RemoveBackground", (handler, view) =>
		{
			if (handler.PlatformView is Android.Widget.EditText editText)
			{
				editText.SetBackgroundColor(Android.Graphics.Color.Transparent);
			}
		});
		return mauiApp;
	}
}
