using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Android.Graphics;
using Android.Views.Accessibility;
using PokerLiveScrapper.Data;
using PokerLiveScrapper.Platforms.Android;

namespace PokerLiveScrapper.Automations
{
    /// <summary>
    /// Poker Live scraping automation using Accessibility Service
    /// </summary>
    public class PokerLiveAutomation
    {
        private readonly AutomationAccessibilityService _accessibilityService;

        public PokerLiveAutomation(AutomationAccessibilityService accessibilityService)
        {
            _accessibilityService = accessibilityService;
        }

        /// <summary>
        /// Scrape tournament data from Poker Live app following the latest flow.
        /// </summary>
        public async Task<(bool success, string message, string jsonData)> ScrapeAsync(string userId, string tournamentFilter, CancellationToken cancellationToken = default)
        {
            try
            {
                Debug.WriteLine($"PokerLiveAutomation: Starting scraping for live '{userId}' (filter: '{tournamentFilter}')");
                cancellationToken.ThrowIfCancellationRequested();

                bool launched = _accessibilityService.CheckForegroundAndLaunchApp(VConstants.POKER_LIVE_APP_PACKAGE);
                if (!launched)
                {
                    return (false, "Failed to launch Poker Live app", string.Empty);
                }

                _accessibilityService.GoBack(10, stopAtHome: true);
                await Task.Delay(1500, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (!await ClickSecondTabAsync(cancellationToken))
                {
                    return (false, "Could not open the event tab", string.Empty);
                }

                var liveNode = await WaitForTextViewAsync(userId, true, retries: 10, delayMs: 1000, cancellationToken);
                if (liveNode == null)
                {
                    return (false, $"Could not find live named '{userId}'", string.Empty);
                }

                _accessibilityService.ClickNode(liveNode);
                await Task.Delay(3000, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                string tabToSelect = ResolveTournamentTabLabel(tournamentFilter);
                if (!await ClickTextViewAsync(tabToSelect, cancellationToken))
                {
                    return (false, $"Could not find '{tabToSelect}' tab", string.Empty);
                }

                await Task.Delay(2000, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                bool isPrevious = tournamentFilter.Trim().Equals("previous", StringComparison.OrdinalIgnoreCase);
                var tournaments = await ScrapeTournamentsFromListAsync(cancellationToken, isPrevious);

                var payload = new Dictionary<string, object>
                {
                    ["live_name"] = userId,
                    ["tournament_filter"] = tournamentFilter,
                    ["tab_selected"] = tabToSelect,
                    ["scraped_at"] = DateTimeOffset.UtcNow,
                    ["tournaments_count"] = tournaments.Count,
                    ["tournaments"] = tournaments
                };

                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(payload, jsonOptions);
                Debug.WriteLine("PokerLiveAutomation: Scraping completed successfully");
                return (true, "Successfully scraped tournaments", json);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("PokerLiveAutomation: Operation cancelled by user");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PokerLiveAutomation: Error - {ex.Message}");
                return (false, $"Error: {ex.Message}", string.Empty);
            }
            finally
            {
                _accessibilityService.GoBack(10, stopAtHome: false);
            }
        }

        private async Task<bool> ClickSecondTabAsync(CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var textViews = _accessibilityService.FindNodesByClassName(PokerLiveConstants.TEXTVIEW_CLASS_NAME);
                var visible = _accessibilityService.FilterVisibleNodes(textViews);

                if (visible.Count >= 2)
                {
                    AccessibilityNodeInfo? target = null;

                    if (visible.Count >= 4)
                    {
                        int index = Math.Max(visible.Count - 4, 0);
                        target = visible.ElementAtOrDefault(index);
                        if (target != null)
                        {
                            string text = target.Text?.ToString() ?? string.Empty;
                            Debug.WriteLine($"PokerLiveAutomation: Attempting to click tab '{text}'");
                        }
                    }

                    target ??= visible.ElementAtOrDefault(1);

                    if (target != null && _accessibilityService.ClickNode(target))
                    {
                        Debug.WriteLine($"PokerLiveAutomation: Attempting to click tab '{target.Text?.ToString()}'");
                        await Task.Delay(2000, cancellationToken);
                        return true;
                    }
                }

                await Task.Delay(500, cancellationToken);
            }

            return false;
        }

        private async Task<AccessibilityNodeInfo?> WaitForTextViewAsync(string text, bool exactMatch, int retries, int delayMs, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < retries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var node = FindVisibleTextViewByText(text, exactMatch);
                if (node != null)
                {
                    return node;
                }

                //scroll down to load more items
                bool scrolled = _accessibilityService.ScrollDown();
                if (!scrolled)
                {
                    break;//end of page reached
                }

                await Task.Delay(delayMs, cancellationToken);
            }

            return null;
        }

        private string ResolveTournamentTabLabel(string tournamentFilter)
        {
            if (string.IsNullOrWhiteSpace(tournamentFilter))
            {
                return "Tournaments";
            }

            if (tournamentFilter.Trim().Equals("previous", StringComparison.OrdinalIgnoreCase))
            {
                return "Previous Tournaments";
            }

            return "Tournaments";
        }

        private async Task<bool> ClickTextViewAsync(string text, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var node = FindVisibleTextViewByText(text, true);
                if (node != null && _accessibilityService.ClickNode(node))
                {
                    await Task.Delay(1500, cancellationToken);
                    return true;
                }

                await Task.Delay(500, cancellationToken);
            }

            return false;
        }

        private AccessibilityNodeInfo? FindVisibleTextViewByText(string text, bool exactMatch)
        {
            var textViews = _accessibilityService.FindNodesByClassName(PokerLiveConstants.TEXTVIEW_CLASS_NAME);
            var visible = _accessibilityService.FilterVisibleNodes(textViews);

            return exactMatch
                ? visible.FirstOrDefault(node => string.Equals(node.Text?.ToString()?.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase))
                : visible.FirstOrDefault(node => node.Text?.ToString()?.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private List<AccessibilityNodeInfo> GetOrderedVisibleTextViews()
        {
            var nodes = _accessibilityService.FindNodesByClassName(PokerLiveConstants.TEXTVIEW_CLASS_NAME);
            return _accessibilityService.FilterVisibleNodes(nodes);
        }

        private string? GetTextAtIndex(List<AccessibilityNodeInfo> nodes, int oneBasedIndex)
        {
            if (oneBasedIndex <= 0)
                return null;

            var node = nodes.ElementAtOrDefault(oneBasedIndex - 1);
            return node?.Text?.ToString();
        }

        private Dictionary<string, string> ExtractDetailPairs(List<AccessibilityNodeInfo> textViews)
        {
            var details = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int pair = 0; pair < 9; pair++)
            {
                int keyIndex = 6 + pair * 2;
                int valueIndex = keyIndex + 1;

                string? key = GetTextAtIndex(textViews, keyIndex)?.Trim();
                string? value = GetTextAtIndex(textViews, valueIndex)?.Trim();

                if (string.IsNullOrWhiteSpace(key))
                    continue;

                details[key] = value ?? string.Empty;
            }

            return details;
        }

        private List<AccessibilityNodeInfo> GetBuyInNodes()
        {
            var textViews = _accessibilityService.FindNodesByClassName(PokerLiveConstants.TEXTVIEW_CLASS_NAME);
            var visible = _accessibilityService.FilterVisibleNodes(textViews);
            return visible.Where(node => node.Text?.ToString()?.IndexOf("Buy In:", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        private string BuildNodeIdentifier(AccessibilityNodeInfo node)
        {
            var rect = new Android.Graphics.Rect();
            node.GetBoundsInScreen(rect);
            string text = node.Text?.ToString()?.Trim() ?? string.Empty;
            return $"{text}|{rect.Top}|{rect.Bottom}";
        }

        private async Task<List<Dictionary<string, object>>> ScrapeTournamentsFromListAsync(CancellationToken cancellationToken, bool isPrevious)
        {
            var tournaments = new List<Dictionary<string, object>>();
            var processedIds = new HashSet<string>(StringComparer.Ordinal);

            int scrollAttempts = 0;
            const int maxScrollAttempts = 8;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(500, cancellationToken);

                var buyInNodes = GetBuyInNodes();
                var candidates = buyInNodes
                    .Select(node => new { Node = node, Id = BuildNodeIdentifier(node) })
                    .Where(x => !processedIds.Contains(x.Id))
                    .ToList();

                if (candidates.Count == 0)
                {
                    if (scrollAttempts >= maxScrollAttempts)
                    {
                        break;
                    }

                    bool scrolled = _accessibilityService.ScrollDown();
                    if (!scrolled)
                    {
                        break; //end of page
                    }

                    scrollAttempts++;
                    await Task.Delay(2000, cancellationToken);
                    continue;
                }

                scrollAttempts = 0;
                var current = candidates[0];
                processedIds.Add(current.Id);

                if (!_accessibilityService.ClickNode(current.Node))
                {
                    var parent = current.Node.Parent;
                    if (parent == null || !_accessibilityService.ClickNode(parent))
                    {
                        Debug.WriteLine("PokerLiveAutomation: Failed to click tournament item");
                        continue;
                    }
                }

                await Task.Delay(3000, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var tournamentData = await ScrapeTournamentDetailPagesAsync(cancellationToken, isPrevious);
                if (tournamentData != null)
                {
                    tournaments.Add(tournamentData);
                }

                _accessibilityService.GoBack(1, stopAtHome: false);
                await Task.Delay(1500, cancellationToken);
            }

            return tournaments;
        }

        private async Task<Dictionary<string, object>?> ScrapeTournamentDetailPagesAsync(CancellationToken cancellationToken, bool isPrevious)
        {
            var textViews = GetOrderedVisibleTextViews();
            if (textViews.Count == 0)
            {
                Debug.WriteLine("PokerLiveAutomation: No detail text views found");
                return null;
            }

            string? title = GetTextAtIndex(textViews, 5)?.Trim();
            if (isPrevious)
            {
                //previous tournaments have "Prizes" at the tabs
                title = GetTextAtIndex(textViews, 6)?.Trim();
            }

            var tournament = new Dictionary<string, object>
            {
                ["title"] = title ?? string.Empty,
                ["details"] = ExtractDetailPairs(textViews)
            };

            tournament["structure"] = await ExtractStructureAsync(cancellationToken);
            if (isPrevious)
            {
                tournament["prizes"] = await ExtractPrizesAsync(cancellationToken);
            }
            tournament["entries"] = await ExtractEntriesAsync(cancellationToken);

            return tournament;
        }

        private async Task<List<Dictionary<string, object>>> ExtractStructureAsync(CancellationToken cancellationToken)
        {
            if (!await ClickTabByContentDescriptionAsync("Structure", cancellationToken))
            {
                Debug.WriteLine("PokerLiveAutomation: Structure tab not found");
                return new List<Dictionary<string, object>>();
            }

            await Task.Delay(1500, cancellationToken);
            var texts = await CollectTextsWithScrollingAsync(6, cancellationToken);
            return ParseStructure(texts);
        }

        private async Task<List<Dictionary<string, string>>> ExtractPrizesAsync(CancellationToken cancellationToken)
        {
            if (!await ClickTabByContentDescriptionAsync("Prizes", cancellationToken))
            {
                Debug.WriteLine("PokerLiveAutomation: Prizes tab not found");
                return new List<Dictionary<string, string>>();
            }

            await Task.Delay(1500, cancellationToken);
            var texts = await CollectTextsWithScrollingAsync(4, cancellationToken);
            return ParseFlatRows(texts, new[] { "Pos", "Winnings", "Name" });
        }

        private async Task<List<Dictionary<string, string>>> ExtractEntriesAsync(CancellationToken cancellationToken)
        {
            if (!await ClickTabByContentDescriptionAsync("Entries", cancellationToken))
            {
                Debug.WriteLine("PokerLiveAutomation: Entries tab not found");
                return new List<Dictionary<string, string>>();
            }

            await Task.Delay(1500, cancellationToken);
            var texts = await CollectTextsWithScrollingAsync(6, cancellationToken);
            return ParseFlatRows(texts, new[] { "Name", "Table", "Seat", "Chips" });
        }

        private async Task<bool> ClickTabByContentDescriptionAsync(string contentDesc, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var viewNodes = _accessibilityService.FindNodesByClassName("android.view.View");
                var visibleViews = _accessibilityService.FilterVisibleNodes(viewNodes);
                var targetView = visibleViews.FirstOrDefault(node => string.Equals(node.ContentDescription?.ToString(), contentDesc, StringComparison.OrdinalIgnoreCase));

                if (targetView != null && _accessibilityService.ClickNode(targetView))
                {
                    return true;
                }

                var textNode = FindVisibleTextViewByText(contentDesc, true);
                if (textNode != null && _accessibilityService.ClickNode(textNode))
                {
                    return true;
                }

                await Task.Delay(500, cancellationToken);
            }

            return false;
        }

        private async Task<List<string>> CollectTextsWithScrollingAsync(int maxScrollAttempts, CancellationToken cancellationToken)
        {
            var collected = new List<string>();
            string? lastScreenLast = null;
            int attempts = 0;

            while (attempts <= maxScrollAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var textViews = GetOrderedVisibleTextViews();
                var texts = textViews
                    .Select(x => x.Text?.ToString()?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (texts.Count == 0)
                {
                    break;
                }

                int startIndex = 0;
                if (collected.Count > 0)
                {
                    var lastCollected = collected.Last();
                    int idx = texts.FindLastIndex(t => t.Equals(lastCollected, StringComparison.Ordinal));
                    if (idx >= 0)
                    {
                        startIndex = idx + 1;
                    }
                }

                for (int i = startIndex; i < texts.Count; i++)
                {
                    var text = texts[i];
                    if (text != null && (collected.Count == 0 || !collected.Last().Equals(text, StringComparison.Ordinal)))
                    {
                        collected.Add(text);
                    }
                }

                var screenLast = texts.Last();
                if (screenLast != null && screenLast.Equals(lastScreenLast, StringComparison.Ordinal))
                {
                    break;
                }

                if (attempts == maxScrollAttempts)
                {
                    break;
                }

                bool scrolled = _accessibilityService.ScrollDown();
                if (!scrolled)
                {
                    scrolled = _accessibilityService.SwipeUp();
                }

                if (!scrolled)
                {
                    break;
                }

                lastScreenLast = screenLast;
                attempts++;
                await Task.Delay(1600, cancellationToken);
            }

            return collected;
        }

        private List<Dictionary<string, string>> ParseFlatRows(List<string> texts, string[] columnNames)
        {
            var results = new List<Dictionary<string, string>>();
            if (texts.Count == 0)
            {
                return results;
            }

            int columns = columnNames.Length;
            var normalizedHeaders = columnNames.Select(c => c.Trim()).ToArray();
            var sanitized = texts.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

            int index = 0;
            while (index + columns - 1 < sanitized.Count)
            {
                var slice = sanitized.Skip(index).Take(columns).ToList();

                bool isHeader = slice.Select(s => s.Trim().ToLowerInvariant())
                                     .SequenceEqual(normalizedHeaders.Select(h => h.ToLowerInvariant()));
                if (isHeader)
                {
                    index += columns;
                    continue;
                }

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < columns; i++)
                {
                    row[normalizedHeaders[i]] = slice[i];
                }

                results.Add(row);
                index += columns;
            }

            return results;
        }

        private List<Dictionary<string, object>> ParseStructure(List<string> texts)
        {
            var sections = new List<Dictionary<string, object>>();
            if (texts.Count == 0)
            {
                return sections;
            }

            var sanitized = texts.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            var columnNames = new[] { "Level", "Blinds", "Ante", "Clock" };

            int index = 0;
            while (index + 3 < sanitized.Count && !IsHeaderRow(sanitized, index, columnNames))
            {
                index++;
            }

            if (index + 3 < sanitized.Count)
            {
                index += 4;
            }
            else
            {
                index = 0;
            }

            var currentSection = new StructureSection("default");

            while (index < sanitized.Count)
            {
                string value = sanitized[index];

                if (ContainsMinText(value) && !IsColumnName(value, columnNames))
                {
                    if (currentSection.Rows.Count > 0 || !sections.Any())
                    {
                        sections.Add(currentSection.ToDictionary());
                    }

                    currentSection = new StructureSection(value.Trim());
                    index++;
                    continue;
                }

                if (index + 3 >= sanitized.Count)
                {
                    break;
                }

                var slice = sanitized.Skip(index).Take(4).ToList();

                if (IsHeaderSlice(slice, columnNames))
                {
                    index += 4;
                    continue;
                }

                currentSection.Rows.Add(new StructureRow
                {
                    Level = slice[0],
                    Blinds = slice[1],
                    Ante = slice[2],
                    Clock = slice[3]
                });

                index += 4;
            }

            if (currentSection.Rows.Count > 0 || !sections.Any())
            {
                sections.Add(currentSection.ToDictionary());
            }

            return sections;
        }

        private bool ContainsMinText(string value)
        {
            return value.IndexOf("min", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsColumnName(string value, IEnumerable<string> columnNames)
        {
            return columnNames.Any(name => name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private bool IsHeaderRow(List<string> texts, int index, string[] columnNames)
        {
            if (index + columnNames.Length - 1 >= texts.Count)
                return false;

            var slice = texts.Skip(index).Take(columnNames.Length).ToList();
            return IsHeaderSlice(slice, columnNames);
        }

        private bool IsHeaderSlice(List<string> slice, string[] columnNames)
        {
            if (slice.Count != columnNames.Length)
                return false;

            return slice.Select(t => t.Trim().ToLowerInvariant())
                        .SequenceEqual(columnNames.Select(c => c.Trim().ToLowerInvariant()));
        }

        private class StructureSection
        {
            public StructureSection(string name)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "default" : name;
                Rows = new List<StructureRow>();
            }

            public string Name { get; }
            public List<StructureRow> Rows { get; }

            public Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    ["name"] = Name,
                    ["levels"] = Rows.Select(row => row.ToDictionary()).ToList()
                };
            }
        }

        private class StructureRow
        {
            public string Level { get; set; } = string.Empty;
            public string Blinds { get; set; } = string.Empty;
            public string Ante { get; set; } = string.Empty;
            public string Clock { get; set; } = string.Empty;

            public Dictionary<string, string> ToDictionary()
            {
                return new Dictionary<string, string>
                {
                    ["level"] = Level,
                    ["blinds"] = Blinds,
                    ["ante"] = Ante,
                    ["clock"] = Clock
                };
            }
        }
    }
}

