using AndroidX.ConstraintLayout.Core.Dsl;
using PokerLiveScrapper.Data;
using PokerLiveScrapper.Interfaces;
using PokerLiveScrapper.Pages;
using PokerLiveScrapper.Platforms.Android;
using PokerLiveScrapper.Services;

namespace PokerLiveScrapper;

public partial class MainPage : ContentPage
{
	private readonly IAutomationService _accessibilityService;
	private bool _isScrapingRunning = false;
	private AutomationService? _automationService;
	private CancellationTokenSource? _scrapingCancellationTokenSource;

	public MainPage(IAutomationService accessibilityService)
	{
		InitializeComponent();
		_accessibilityService = accessibilityService;

		TournamentTypePicker.SelectedIndex = 0;

		GetAppVersionNumber();

		// Initialize automation service
		InitializeAutomationService();

		if (VConstants.IS_TEST_MODE)
		{
			UserIdEntry.Text = "Stoke";
		}
	}

	private void InitializeAutomationService()
	{
		try
		{
			_automationService = new AutomationService();
			_automationService.Initialize();

			System.Diagnostics.Debug.WriteLine("AutomationService initialized successfully");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Failed to initialize AutomationService: {ex.Message}");
		}
	}

	private void GetAppVersionNumber()
	{
        AppTitleLabel.Text = $"Poker Live Scrapper v{AppInfo.VersionString}";
		UpdateStatus();
	}

	private async void OnEnableAccessibilityClicked(object? sender, EventArgs e)
	{
		try
		{
			// Check current status to determine action
			var isEnabled = _accessibilityService.IsAccessibilityServiceEnabled;

			if (isEnabled)
			{
				// Service is enabled, ask user to disable it
				var result = await DisplayAlert(
					"Disable Accessibility Service",
                    "This will open the Accessibility Settings. Please find 'Poker Live Scrapper' and toggle it OFF.",
					"Open Settings",
					"Cancel"
				);

				if (result)
				{
					// Open accessibility settings
					var success = await _accessibilityService.RequestAccessibilityPermissionAsync();

					// Wait a moment for user to make changes
					await Task.Delay(1000);

					// Check status periodically to see if disabled
					for (int i = 0; i < 15; i++)
					{
						await Task.Delay(2000); // Wait 2 seconds
						var currentStatus = _accessibilityService.IsAccessibilityServiceEnabled;
						System.Diagnostics.Debug.WriteLine($"Page: Status check {i + 1}: {currentStatus}");

						if (!currentStatus)
						{
							UpdateStatus();
							await DisplayAlert("Success", "Accessibility service has been disabled.", "OK");
							return;
						}
					}

					// Final check after timeout
					UpdateStatus();
				}
			}
			else
			{
				// Service is disabled, enable it
				var success = await _accessibilityService.RequestAccessibilityPermissionAsync();

				if (success)
				{
					// Check status periodically for up to 30 seconds
					for (int i = 0; i < 15; i++)
					{
						await Task.Delay(2000); // Wait 2 seconds
						var currentStatus = _accessibilityService.IsAccessibilityServiceEnabled;
						System.Diagnostics.Debug.WriteLine($"Page: Status check {i + 1}: {currentStatus}");

						if (currentStatus)
						{
							UpdateStatus();
							return;
						}
					}

					// Final check after timeout
					UpdateStatus();
				}
			}
		}
		catch (System.Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Page: Error in OnEnableAccessibilityClicked: {ex.Message}");
		}
	}

	private void UpdateStatus()
	{
		var isEnabled = _accessibilityService.IsAccessibilityServiceEnabled;
		StatusLabel.Text = $"Accessibility Service Status: {(isEnabled ? "ENABLED" : "DISABLED")}";

        StatusLabel.TextColor = isEnabled
            ? Color.FromArgb("#0B6E4F")
            : Color.FromArgb("#C84B31");

		// Update button text and color based on status
		if (isEnabled)
		{
            EnableAccessibilityBtn.Text = "Disable";
            EnableAccessibilityBtn.BackgroundColor = Color.FromArgb("#C84B31"); // Ember red
			StartScrapingBtn.IsEnabled = true;
		}
		else
		{
            EnableAccessibilityBtn.Text = "Enable";
            EnableAccessibilityBtn.BackgroundColor = Color.FromArgb("#F4E285"); // Warm gold
			StartScrapingBtn.IsEnabled = false;
		}

		// Initialize automation service when enabled
		if (isEnabled)
		{
			InitializeAutomationService();
		}
	}

	private async void OnCopyJsonClicked(object? sender, EventArgs e)
	{
		var jsonText = JsonResponseLabel.Text;
		if (!string.IsNullOrEmpty(jsonText))
		{
			await Clipboard.Default.SetTextAsync(jsonText);
			await DisplayAlert("Copied", "JSON data copied to clipboard!", "OK");
		}
	}

	private async void OnViewFullScreenClicked(object? sender, EventArgs e)
	{
		var jsonText = JsonResponseLabel.Text;
		if (!string.IsNullOrEmpty(jsonText))
		{
			var jsonViewerPage = new JsonViewerPage(jsonText);
			await Navigation.PushModalAsync(jsonViewerPage);
		}
		else
		{
			await DisplayAlert("No Data", "No JSON data to display. Please run scraping first.", "OK");
		}
	}

	private async void OnStartScrapingClicked(object? sender, EventArgs e)
	{
		var service = AutomationAccessibilityService.Instance;
		if (service?.IsServiceRunning() != true)
		{
			await DisplayAlert("Error", "Accessibility service is not running. Please enable it in Settings.", "OK");
			return;
		}

		if (_automationService == null)
			InitializeAutomationService(); // Try to initialize again
		if (_automationService == null)
		{
			await DisplayAlert("Error", "Automation service is not initialized.", "OK");
			return;
		}

		// Get user ID from input
		var userId = UserIdEntry.Text?.Trim();
		if (string.IsNullOrEmpty(userId))
		{
			await DisplayAlert("Error", "Please enter a user ID to scrape.", "OK");
			return;
		}

		var tournamentType = TournamentTypePicker.SelectedItem?.ToString();
		if (string.IsNullOrEmpty(tournamentType))
		{
			await DisplayAlert("Error", "Please select a tournament list.", "OK");
			return;
		}

		try
		{
			if (_isScrapingRunning)
			{
				// Stop scraping - request cancellation
				StartScrapingBtn.Text = "⏹️ Stopping...";
				StartScrapingBtn.IsEnabled = false;

				// Cancel the scraping
				_scrapingCancellationTokenSource?.Cancel();

				return;
			}
			else
			{
				// Start scraping on a new thread
				_isScrapingRunning = true;

				// Change button text immediately to show it can be stopped
				StartScrapingBtn.Text = "🛑 Stop";
				StartScrapingBtn.IsEnabled = true;

				// Clear previous JSON result
				JsonResponseLabel.Text = "";

				// Show loading indicator
				LoadingIndicator.IsVisible = true;
				LoadingIndicator.IsRunning = true;

				// Create new cancellation token source
				_scrapingCancellationTokenSource = new CancellationTokenSource();
				var cancellationToken = _scrapingCancellationTokenSource.Token;

				// Run scraping on a separate thread
				_ = Task.Run(async () =>
				{
					try
					{
						var result = await _automationService.RunScrapingAsync(userId, tournamentType, cancellationToken);

						// Check if cancelled
						if (cancellationToken.IsCancellationRequested)
						{
							System.Diagnostics.Debug.WriteLine("Scraping was cancelled");

							MainThread.BeginInvokeOnMainThread(async () =>
							{
								await RestoreUIAfterScraping(false);
								await DisplayAlert("Scraping Stopped", "The scraping process was stopped by user.", "OK");
							});
						}
						else
						{
							// Scraping completed successfully
							MainThread.BeginInvokeOnMainThread(async () =>
							{
								await RestoreUIAfterScraping(true);

								if (result.success)
								{
									JsonResponseLabel.Text = result.jsonData;
									await DisplayAlert("Success", "Scraping completed successfully!", "OK");
								}
								else
								{
									await DisplayAlert("Error", $"Scraping failed: {result.message}", "OK");
								}
							});
						}
					}
					catch (OperationCanceledException)
					{
						System.Diagnostics.Debug.WriteLine("Scraping was cancelled via exception");

						MainThread.BeginInvokeOnMainThread(async () =>
						{
							await RestoreUIAfterScraping(false);
							await DisplayAlert("Scraping Stopped", "The scraping process was stopped.", "OK");
						});
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Error in scraping: {ex.Message}");

						MainThread.BeginInvokeOnMainThread(async () =>
						{
							await RestoreUIAfterScraping(false);
							await DisplayAlert("Error", $"Failed to run scraping: {ex.Message}", "OK");
						});
					}
				}, cancellationToken);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error in scraping setup: {ex.Message}");
			await RestoreUIAfterScraping(false);
			await DisplayAlert("Error", $"Failed to start scraping: {ex.Message}", "OK");
		}
	}

	private async Task RestoreUIAfterScraping(bool showResult)
	{
		// Hide loading indicator
		LoadingIndicator.IsVisible = false;
		LoadingIndicator.IsRunning = false;

		// Reset button state
		StartScrapingBtn.Text = "START";
		StartScrapingBtn.IsEnabled = true;
		_isScrapingRunning = false;

		// Clean up cancellation token
		_scrapingCancellationTokenSource?.Dispose();
		_scrapingCancellationTokenSource = null;

		await Task.CompletedTask;
	}
	
	private async void OnWebsiteTapped(object? sender, EventArgs e)
	{
		Label? label = sender as Label;
		if (label == null)
			return;

		string url = label.Text;
		if (!url.StartsWith("http"))
		{
			url = "https://" + url;
		}
		await Browser.OpenAsync(url);
	}

}
