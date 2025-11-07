using Android.Content;
using Android.Provider;
using Android.Views.Accessibility;
using PokerLiveScrapper.Data;
using PokerLiveScrapper.Interfaces;

namespace PokerLiveScrapper.Platforms.Android
{
    public class AndroidAutomationService : IAutomationService
    {
        public bool IsAccessibilityServiceEnabled
        {
            get
            {
                try
                {
                    var context = global::Android.App.Application.Context;
                    if (context == null)
                    {
                        System.Diagnostics.Debug.WriteLine("AndroidAutomationService: Context is null");
                        return false;
                    }

                    var accessibilityManager = context.GetSystemService(Context.AccessibilityService) as AccessibilityManager;
                    if (accessibilityManager == null)
                    {
                        System.Diagnostics.Debug.WriteLine("AndroidAutomationService: AccessibilityManager is null");
                        return false;
                    }

                    var enabledServices = Settings.Secure.GetString(
                        context.ContentResolver,
                        Settings.Secure.EnabledAccessibilityServices);

                    System.Diagnostics.Debug.WriteLine($"AndroidAutomationService: All enabled services: '{enabledServices}'");

                    if (string.IsNullOrEmpty(enabledServices))
                    {
                        System.Diagnostics.Debug.WriteLine("AndroidAutomationService: No enabled services found");
                        return false;
                    }

                    // Try multiple possible service name formats
                    var possibleServiceNames = new[]
                    {
                        VConstants.BIGO_LIVE_SCRAPPER_PACKAGE + "/crc641154dc6b6e1fbd62.AutomationAccessibilityService"
                    };

                    foreach (var serviceName in possibleServiceNames)
                    {
                        if (enabledServices.Contains(serviceName))
                        {
                            System.Diagnostics.Debug.WriteLine($"AndroidAutomationService: Found service: {serviceName}");
                            return true;
                        }
                    }

                    System.Diagnostics.Debug.WriteLine("AndroidAutomationService: Service not found in enabled services");
                    return false;
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AndroidAutomationService: Error checking accessibility service: {ex.Message}");
                    return false;
                }
            }
        }

        public async Task<bool> RequestAccessibilityPermissionAsync()
        {
            try
            {
                var context = global::Android.App.Application.Context;
                Intent intent;

                // Try multiple approaches to open accessibility settings
                // Approach 1: Try to open specific accessibility service settings
                try
                {
                    intent = new Intent(Settings.ActionAccessibilitySettings);
                    intent.SetFlags(ActivityFlags.NewTask);

                    // Add component name to try to go directly to our service
                    var componentName = new ComponentName(
                        VConstants.BIGO_LIVE_SCRAPPER_PACKAGE,
                        "restaurantpostgenerator.platforms.android.AutomationAccessibilityService");
                    intent.PutExtra("component_name", componentName.FlattenToString());

                    context.StartActivity(intent);
                    await Task.Delay(1000);
                    return true;
                }
                catch (ActivityNotFoundException)
                {
                    System.Diagnostics.Debug.WriteLine("Standard accessibility settings not available, trying alternative...");
                }
                catch (Java.Lang.SecurityException)
                {
                    System.Diagnostics.Debug.WriteLine("Accessibility settings restricted, trying alternative...");
                }

                // Approach 2: Try opening general app settings
                try
                {
                    intent = new Intent(Settings.ActionApplicationDetailsSettings);
                    intent.SetData(global::Android.Net.Uri.Parse("package:" + VConstants.BIGO_LIVE_SCRAPPER_PACKAGE));
                    intent.SetFlags(ActivityFlags.NewTask);
                    context.StartActivity(intent);
                    await Task.Delay(1000);
                    return true;
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"App settings also failed: {ex2.Message}");
                }

                // Approach 3: Try opening general settings
                try
                {
                    intent = new Intent(Settings.ActionSettings);
                    intent.SetFlags(ActivityFlags.NewTask);
                    context.StartActivity(intent);
                    await Task.Delay(1000);
                    return true;
                }
                catch (Exception ex3)
                {
                    System.Diagnostics.Debug.WriteLine($"General settings also failed: {ex3.Message}");
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error requesting accessibility permission: {ex.Message}");
                return false;
            }
        }

    }
}