using PokerLiveScrapper.Automations;
using PokerLiveScrapper.Platforms.Android;

namespace PokerLiveScrapper.Services
{
    /// <summary>
    /// Centralized service to manage Bigo Live scraping automation
    /// </summary>
    public class AutomationService
    {
        private readonly AutomationAccessibilityService _accessibilityService;
        private BigoLiveAutomation? _bigoLiveAutomation;

        public AutomationService()
        {
            _accessibilityService = AutomationAccessibilityService.Instance 
                ?? throw new InvalidOperationException("Accessibility service is not running");
        }

        /// <summary>
        /// Initialize all automation instances
        /// </summary>
        public void Initialize()
        {
            _bigoLiveAutomation = new BigoLiveAutomation(_accessibilityService);
        }

        /// <summary>
        /// Run scraping automation with cancellation support
        /// </summary>
        public async Task<(bool success, string message, string jsonData)> RunScrapingAsync(string userId, CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine("AutomationService: Starting scraping automation");

            if (_bigoLiveAutomation == null)
            {
                Initialize();
            }

            if (_bigoLiveAutomation == null)
            {
                return (false, "BigoLive automation not initialized", "");
            }

            try
            {
                // Check if cancelled before starting
                if (cancellationToken.IsCancellationRequested)
                {
                    return (false, "Scraping cancelled by user", "");
                }

                System.Diagnostics.Debug.WriteLine($"AutomationService: Running scraping for user ID: {userId}");
                
                var result = await _bigoLiveAutomation.ScrapeAsync(userId, cancellationToken);
                
                // Check if cancelled after completion
                if (cancellationToken.IsCancellationRequested)
                {
                    return (false, "Scraping cancelled by user", "");
                }
                
                return result;
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("AutomationService: Scraping cancelled");
                return (false, "Scraping cancelled by user", "");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationService: Error in scraping - {ex.Message}");
                return (false, $"Exception: {ex.Message}", "");
            }
        }
    }
}
