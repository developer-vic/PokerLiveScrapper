using System.Threading;
using PokerLiveScrapper.Automations;
using PokerLiveScrapper.Platforms.Android;
using System.Threading;
using System.Threading.Tasks;

namespace PokerLiveScrapper.Services
{
    /// <summary>
    /// Centralized service to manage Poker Live scraping automation
    /// </summary>
    public class AutomationService
    {
        private readonly AutomationAccessibilityService _accessibilityService;
        private PokerLiveAutomation? _pokerLiveAutomation;

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
            _pokerLiveAutomation = new PokerLiveAutomation(_accessibilityService);
        }

        /// <summary>
        /// Run scraping automation with cancellation support
        /// </summary>
        public async Task<(bool success, string message, string jsonData)> RunScrapingAsync(string liveName, string tournamentFilter, CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine("AutomationService: Starting scraping automation");

            if (_pokerLiveAutomation == null)
            {
                Initialize();
            }

            if (_pokerLiveAutomation == null)
            {
                return (false, "PokerLive automation not initialized", "");
            }

            try
            {
                // Check if cancelled before starting
                if (cancellationToken.IsCancellationRequested)
                {
                    return (false, "Scraping cancelled by user", "");
                }

                System.Diagnostics.Debug.WriteLine($"AutomationService: Running scraping for live name: {liveName} with filter: {tournamentFilter}");
                
                var result = await _pokerLiveAutomation.ScrapeAsync(liveName, tournamentFilter, cancellationToken);
                
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
