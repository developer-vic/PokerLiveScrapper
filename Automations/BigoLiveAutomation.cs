using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Http;
using PokerLiveScrapper.Data;
using PokerLiveScrapper.Platforms.Android;
using Android.Views.Accessibility;

namespace PokerLiveScrapper.Automations
{
    /// <summary>
    /// Bigo Live scraping automation using Accessibility Service
    /// </summary>
    public class BigoLiveAutomation
    {
        private readonly AutomationAccessibilityService _accessibilityService;

        public BigoLiveAutomation(AutomationAccessibilityService accessibilityService)
        {
            _accessibilityService = accessibilityService;
        }

        /// <summary>
        /// Scrape contribution ranking data from Bigo Live app
        /// </summary>
        public async Task<(bool success, string message, string jsonData)> ScrapeAsync(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"BigoLiveAutomation: Starting scraping for user ID: {userId}");

                // Check cancellation
                cancellationToken.ThrowIfCancellationRequested();

                // Launch Bigo Live app
                bool launched = _accessibilityService.CheckForegroundAndLaunchApp(BigoLiveSConstants.PACKAGE_NAME);
                if (!launched)
                {
                    return (false, "Failed to launch Bigo Live app", "");
                }
                // Navigate to home/feed
                _accessibilityService.GoBack(10, stopAtHome: true);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 1: Find and click search button (sg.bigo.live:id/iv_search)
                System.Diagnostics.Debug.WriteLine("BigoLiveAutomation: Looking for search button");
                var searchButton = _accessibilityService.FindNodeByResourceId(null, BigoLiveSConstants.SEARCH_BUTTON_ID);
                if (searchButton == null)
                {
                    return (false, "Could not find search button", "");
                }
                _accessibilityService.ClickNode(searchButton);
                await Task.Delay(1000, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 2: Find search input (sg.bigo.live:id/searchInput) - First android.widget.EditText
                System.Diagnostics.Debug.WriteLine("BigoLiveAutomation: Looking for search input");
                var searchInput = _accessibilityService.FindNodeByResourceId(null, BigoLiveSConstants.SEARCH_INPUT_ID);
                if (searchInput == null)
                {
                    // Fallback: try to find first EditText
                    var editTexts = _accessibilityService.FindNodesByClassName("android.widget.EditText");
                    if (editTexts == null || editTexts.Count == 0)
                    {
                        return (false, "Could not find search input field", "");
                    }
                    searchInput = editTexts[0];
                }

                // Click and enter username
                _accessibilityService.ClickNode(searchInput);
                await Task.Delay(200, cancellationToken);
                _accessibilityService.InputText(userId, 0);
                await Task.Delay(200, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 3: Back(1) to hide keyboard
                System.Diagnostics.Debug.WriteLine("BigoLiveAutomation: Hiding keyboard");
                _accessibilityService.GoBack(1, stopAtHome: false);
                await Task.Delay(200, cancellationToken);
                _accessibilityService.ClickByResourceId(BigoLiveSConstants.SEARCH_CONFIRM_ID);
                await Task.Delay(2000, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 4: Click second tab (Host) - sg.bigo.live:id/uiTabTitle index 1
                System.Diagnostics.Debug.WriteLine("BigoLiveAutomation: Looking for search result");
                var searchResult = _accessibilityService.FindNodeByResourceId(null, BigoLiveSConstants.SEARCH_HOST_TABS_ID, 1);
                if (searchResult == null)
                {
                    return (false, "Could not find Host tab", "");
                }
                _accessibilityService.ClickNode(searchResult);
                await Task.Delay(5000, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                
                //Find first profile picture and click it SEARCH_HOST_PICS_ID
                System.Diagnostics.Debug.WriteLine("BigoLiveAutomation: Looking for host profile picture");
                var profilePic = _accessibilityService.FindNodeByResourceId(null, BigoLiveSConstants.SEARCH_HOST_PICS_ID, 0);
                if (profilePic == null)
                {
                    return (false, "Could not find host profile picture", "");
                }
                _accessibilityService.ClickNode(profilePic);
                await Task.Delay(2000, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 5: Click contribution entry (sg.bigo.live:id/fl_contrib_entry -> sg.bigo.live:id/tv_contribute)
                System.Diagnostics.Debug.WriteLine("BigoLiveAutomation: Looking for contribution entry");
                var contribEntry = _accessibilityService.FindNodeByResourceId(null, BigoLiveSConstants.CONTRIB_ENTRY_ID);
                if (contribEntry == null)
                {
                    // Try clicking the text directly
                    var contribText = _accessibilityService.FindNodeByResourceId(null, BigoLiveSConstants.CONTRIB_TEXT_ID);
                    if (contribText == null)
                    {
                        return (false, "Could not find contribution entry", "");
                    }
                    _accessibilityService.ClickNode(contribText);
                }
                else
                {
                    _accessibilityService.ClickNode(contribEntry);
                }
                await Task.Delay(3000, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 6: Find all 4 tabs (sg.bigo.live:id/uiTabTitle) - Daily, Weekly, Monthly, Overall
                System.Diagnostics.Debug.WriteLine("BigoLiveAutomation: Looking for tabs");
                var scrapedData = new Dictionary<string, object>();

                var tabDataDict = new Dictionary<string, List<Dictionary<string, object>>>();
                var summaryDict = new Dictionary<string, object>();

                // Define tab order and max items
                var tabConfigs = new[]
                {
                    new { Name = "Daily", MaxItems = 3 },
                    new { Name = "Weekly", MaxItems = 3 },
                    new { Name = "Monthly", MaxItems = 3 },
                    new { Name = "Overall", MaxItems = VConstants.IS_TEST_MODE ? 3 : 10 }
                };

                int totalUsersScraped = 0;
                int totalTabsScraped = 0;

                for (int tabIndex = 0; tabIndex < Math.Min(tabConfigs.Length, 4); tabIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var tabConfig = tabConfigs[tabIndex];

                    System.Diagnostics.Debug.WriteLine($"BigoLiveAutomation: Scraping {tabConfig.Name} tab");

                    // Click the tab
                    var nextTab = _accessibilityService.FindNodeByResourceId(null,
                        BigoLiveSConstants.TAB_TITLE_ID, tabIndex);
                    if (nextTab == null) continue;

                    _accessibilityService.ClickNode(nextTab);
                    await Task.Delay(2000, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    // Scrape data for this tab (pass fresh rootNode context)
                    var tabData = await ScrapeTabData(tabConfig.MaxItems, cancellationToken);
                    tabDataDict[tabConfig.Name] = tabData;
                    
                    totalUsersScraped += tabData.Count;
                    if (tabData.Count > 0)
                        totalTabsScraped++;

                    await Task.Delay(1000, cancellationToken);
                }

                // Build summary
                summaryDict["total_users_scraped"] = totalUsersScraped;
                summaryDict["total_tabs_scraped"] = totalTabsScraped;
                summaryDict["tabs"] = new Dictionary<string, int>
                {
                    { "Daily", tabDataDict.ContainsKey("Daily") ? tabDataDict["Daily"].Count : 0 },
                    { "Weekly", tabDataDict.ContainsKey("Weekly") ? tabDataDict["Weekly"].Count : 0 },
                    { "Monthly", tabDataDict.ContainsKey("Monthly") ? tabDataDict["Monthly"].Count : 0 },
                    { "Overall", tabDataDict.ContainsKey("Overall") ? tabDataDict["Overall"].Count : 0 }
                };


                // Build final JSON structure with summary, data, and searched username
                scrapedData["searched_username"] = userId;
                scrapedData["summary"] = summaryDict;
                scrapedData["data"] = tabDataDict;

                // Convert to JSON
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                string jsonData = JsonSerializer.Serialize(scrapedData, jsonOptions);

                System.Diagnostics.Debug.WriteLine("BigoLiveAutomation: Scraping completed successfully");
                return (true, "Successfully scraped data", jsonData);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("BigoLiveAutomation: Operation cancelled by user");
                throw; // Re-throw to be handled by caller
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BigoLiveAutomation: Error - {ex.Message}");
                return (false, $"Error: {ex.Message}", "");
            }
            finally
            {
                // Navigate back to home
                _accessibilityService.GoBack(10, stopAtHome: false);
            }
        }

        /// <summary>
        /// Scrape data from a specific tab
        /// </summary>
        private async Task<List<Dictionary<string, object>>> ScrapeTabData(int maxItems, CancellationToken cancellationToken)
        {
            var results = new List<Dictionary<string, object>>();

            // Wait for tab content to load and get fresh rootNode
            await Task.Delay(1500, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // Get all nodes and filter to only visible ones (workaround for tab switching issue)
            var allUserNameNodes = _accessibilityService.FindNodesByResourceId(BigoLiveSConstants.USER_NAME_ID);
            // Filter to only visible nodes (current tab content)
            var userNameNodes = _accessibilityService.FilterVisibleNodes(allUserNameNodes);
            if (userNameNodes == null || userNameNodes.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("BigoLiveAutomation: No user names found in tab");
                return results;
            }
            
            // Get all nodes and filter to only visible ones (workaround for tab switching issue)
            var allContributionNodes = _accessibilityService.FindNodesByResourceId(BigoLiveSConstants.CONTRIBUTION_AMOUNT_ID);
            var allLevelNodes = _accessibilityService.FindNodesByResourceId(BigoLiveSConstants.USER_LEVEL_ID);
            // Filter to only visible nodes (current tab content)
            var contributionNodes = _accessibilityService.FilterVisibleNodes(allContributionNodes);
            var levelNodes = _accessibilityService.FilterVisibleNodes(allLevelNodes);

            int itemCount = Math.Min(maxItems, userNameNodes.Count);
            System.Diagnostics.Debug.WriteLine($"BigoLiveAutomation: Found {userNameNodes.Count} visible users (from {allUserNameNodes?.Count ?? 0} total), scraping {itemCount} items");

            for (int i = 0; i < itemCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var userData = new Dictionary<string, object>();

                // Get contribution and level data BEFORE clicking (to avoid re-fetching)
                string amount = "";
                string userLevel = "";

                if (contributionNodes != null && i < contributionNodes.Count)
                {
                    amount = contributionNodes[i].Text?.ToString() ?? "";
                }
                if (levelNodes != null && i < levelNodes.Count)
                {
                    userLevel = levelNodes[i].Text?.ToString() ?? "";
                }

                // Get username (display name) from tv_name 
                var userNameNode = userNameNodes[i];
                var rawUsername = userNameNode.Text?.ToString() ?? "";

                // Fix emoji handling - decode unicode escape sequences to actual emojis
                string username = DecodeUnicodeEmojis(rawUsername);

                // Click on the username to go to profile page
                System.Diagnostics.Debug.WriteLine($"BigoLiveAutomation: Clicking username {i + 1}: {username}");
                _accessibilityService.ClickNode(userNameNode);
                await Task.Delay(1000, cancellationToken); // Wait for profile page to load
                cancellationToken.ThrowIfCancellationRequested();

                // Find the actual user_id from tv_bigo_id
                string actualUserId = "";
                var bigoIdNode = _accessibilityService.FindNodeByResourceId(null, BigoLiveSConstants.BIGO_ID_ID);
                if (bigoIdNode != null)
                {
                    var rawUserId = bigoIdNode.Text?.ToString() ?? "";
                    // Extract only the ID value (remove "ID: " prefix if present)
                    actualUserId = ExtractUserIdValue(rawUserId);
                    System.Diagnostics.Debug.WriteLine($"BigoLiveAutomation: Found user_id: {actualUserId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"BigoLiveAutomation: Could not find tv_bigo_id element");
                }

                // Go back to the list
                _accessibilityService.GoBack(1, stopAtHome: false);
                await Task.Delay(200, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Store all data
                userData["user_id"] = actualUserId;
                userData["username"] = username; // Store username separately
                userData["amount"] = amount;
                userData["rank_position"] = i + 1;
                userData["user_level"] = userLevel;

                // Extract profile picture URL using the actual user_id
                if (!string.IsNullOrEmpty(actualUserId))
                {
                    userData["profile_picture_url"] = await ExtractProfilePictureUrlAsync(actualUserId, cancellationToken) ?? "";
                }
                else
                {
                    userData["profile_picture_url"] = "";
                }

                results.Add(userData);
            }

            return results;
        }

        /// <summary>
        /// Extract profile picture URL from Bigo Live web page
        /// Fetches HTML from https://www.bigo.tv/user/{user_id} and extracts first img src from div class="img-preview"
        /// </summary>
        private async Task<string?> ExtractProfilePictureUrlAsync(string user_id, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(user_id))
                    return null;

                // Build URL
                string profileUrl = $"https://www.bigo.tv/user/{Uri.EscapeDataString(user_id)}";
                System.Diagnostics.Debug.WriteLine($"BigoLiveAutomation: Fetching profile picture from: {profileUrl}");

                // Create HttpClient
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                // Fetch HTML
                var response = await httpClient.GetAsync(profileUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"BigoLiveAutomation: Failed to fetch profile page. Status: {response.StatusCode}");
                    return null;
                }

                var htmlContent = await response.Content.ReadAsStringAsync(cancellationToken);

                // Find div with class="img-preview"
                // Pattern: <div class="img-preview"...>...<img src="..."...
                var divPattern = @"<div[^>]*class\s*=\s*[""']img-preview[""'][^>]*>(.*?)</div>";
                var divMatch = Regex.Match(htmlContent, divPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (divMatch.Success)
                {
                    var divContent = divMatch.Groups[1].Value;

                    // Find first img tag src in the div
                    var imgPattern = @"<img[^>]*src\s*=\s*[""']([^""']+)[""']";
                    var imgMatch = Regex.Match(divContent, imgPattern, RegexOptions.IgnoreCase);

                    if (imgMatch.Success)
                    {
                        var imgSrc = imgMatch.Groups[1].Value;

                        // Split by ? and take [0] to remove query parameters
                        var profilePictureUrl = imgSrc.Split('?')[0].Trim();

                        System.Diagnostics.Debug.WriteLine($"BigoLiveAutomation: Found profile picture URL: {profilePictureUrl}");
                        return profilePictureUrl;
                    }
                }

                System.Diagnostics.Debug.WriteLine("BigoLiveAutomation: Could not find img-preview div or img tag");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BigoLiveAutomation: Error extracting profile picture URL: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extract the actual user ID value from text that may contain "ID: " prefix
        /// Example: "ID: RA_H2019" -> "RA_H2019"
        /// </summary>
        private string ExtractUserIdValue(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove "ID: " prefix if present (case-insensitive)
            var trimmed = text.Trim();
            if (trimmed.Contains(":"))
            {
                var array = trimmed.Split(':');
                var result = array.Length > 1 ? array[1].TrimStart() : "";
                return result;
            }

            return trimmed;
        }

        /// <summary>
        /// Decode unicode escape sequences (like \uD83C) to actual emojis
        /// Handles both surrogate pairs and single unicode characters
        /// </summary>
        private string DecodeUnicodeEmojis(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            try
            {
                // Use JSON deserialization to decode all unicode escapes, including surrogate pairs and mixed sequences
                // Wrap the string in quotes to make it a valid JSON string
                string jsonWrapped = $"\"{text.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
                return JsonSerializer.Deserialize<string>(jsonWrapped) ?? text;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BigoLiveAutomation: Error decoding unicode emojis: {ex.Message}");
                return text; // Return original text if decoding fails
            }
        }
    }
}
