using Android.AccessibilityServices;
using Android.Content;
using Android.OS;
using Android.Views.Accessibility;
using AndroidX.Core.View.Accessibility;
using PokerLiveScrapper.Data;
using System.Collections.Concurrent;
using Exception = Java.Lang.Exception;

namespace PokerLiveScrapper.Platforms.Android
{
    /// <summary>
    /// Android Accessibility Service for cross-app automation
    /// Main service that handles UI interactions across different apps
    /// </summary>
    public class AutomationAccessibilityService : AccessibilityService
    {
        public static AutomationAccessibilityService? Instance { get; private set; }

        private readonly ConcurrentQueue<AutomationAction> _actionQueue = new();
        private bool _isServiceRunning;

        public override void OnCreate()
        {
            base.OnCreate();
            Instance = this;
            System.Diagnostics.Debug.WriteLine("AutomationAccessibilityService: Service created and instance set");
        }

        protected override void OnServiceConnected()
        {
            base.OnServiceConnected();
            _isServiceRunning = true;
            System.Diagnostics.Debug.WriteLine("AutomationAccessibilityService: Service connected and ready");

            // Log that the service is connected
            var info = ServiceInfo;
            if (info != null)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Service connected with ID: {info.Id}");
            }
        }

        public override void OnAccessibilityEvent(AccessibilityEvent? e)
        {
            if (e == null) return;

            try
            {
                // Log significant events for debugging
                if (e.EventType == EventTypes.WindowStateChanged ||
                    e.EventType == EventTypes.WindowContentChanged && e.PackageName == VConstants.POKER_LIVE_APP_PACKAGE)
                {
                    System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: {e.EventType} in {e.PackageName}");
                }

                // Process any queued actions
                ProcessQueuedActions();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error in OnAccessibilityEvent: {ex.Message}");
            }
        }

        public override void OnInterrupt()
        {
            System.Diagnostics.Debug.WriteLine("AutomationAccessibilityService: Service interrupted");
        }

        public bool IsServiceRunning()
        {
            return _isServiceRunning && Instance != null;
        }

        // Core automation methods used by services
        public async Task<AccessibilityNodeInfo?> FindNodeByTextAsync(string text, int timeoutMs = 5000)
        {
            var startTime = System.Environment.TickCount;

            while (System.Environment.TickCount - startTime < timeoutMs)
            {
                var rootNode = RootInActiveWindow;
                if (rootNode != null)
                {
                    var node = FindNodeByText(rootNode, text);
                    if (node != null) return node;
                }

                await Task.Delay(500);
            }

            return null;
        }

        public async Task<AccessibilityNodeInfo?> FindNodeByResourceIdAsync(string resourceId, int timeoutMs = 5000)
        {
            var startTime = System.Environment.TickCount;

            while (System.Environment.TickCount - startTime < timeoutMs)
            {
                var rootNode = RootInActiveWindow;
                if (rootNode != null)
                {
                    var node = FindNodeByResourceId(rootNode, resourceId);
                    if (node != null) return node;
                }

                await Task.Delay(500);
            }

            return null;
        }

        public async Task<AccessibilityNodeInfo?> FindNodeByClassNameAsync(string className, int timeoutMs = 5000)
        {
            var startTime = System.Environment.TickCount;

            while (System.Environment.TickCount - startTime < timeoutMs)
            {
                var rootNode = RootInActiveWindow;
                if (rootNode != null)
                {
                    var node = FindNodeByClassName(rootNode, className);
                    if (node != null) return node;
                }

                await Task.Delay(500);
            }

            return null;
        }

        public bool ClickNode(AccessibilityNodeInfo node, bool useNativeClick = false)
        {
            try
            {
                bool clickResult = false;
                if (useNativeClick)
                {
                    clickResult = ClickNodeNative(node);
                    if (clickResult) return true;
                }

                clickResult = node.PerformAction(global::Android.Views.Accessibility.Action.Click);
                if (clickResult) return true;

                if (!node.Clickable && node.Parent != null)
                    clickResult = node.Parent.PerformAction(global::Android.Views.Accessibility.Action.Click);
                if (clickResult) return true;

                return ClickNodeNative(node);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error clicking node: {ex.Message}");
                return false;
            }
        }

        private bool ClickNodeNative(AccessibilityNodeInfo node)
        {
            try
            {
                var path = new global::Android.Graphics.Path();
                var rect = new global::Android.Graphics.Rect();
                node.GetBoundsInScreen(rect);
                path.MoveTo(rect.CenterX(), rect.CenterY());

                var builder = new GestureDescription.Builder();
                if (builder == null) return false;
                var description = new GestureDescription.StrokeDescription(path, 0, 100);
                if (description == null) return false;
                var stroke = builder.AddStroke(description);
                if (stroke == null) return false;
                var gesture = stroke.Build();
                if (gesture == null) return false;
                DispatchGesture(gesture, null, null);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error in native click: {ex.Message}");
                return false;
            }
        }

        public bool SetTextInNode(AccessibilityNodeInfo node, string text)
        {
            try
            {
                var bundle = new Bundle();
                bundle.PutCharSequence(AccessibilityNodeInfoCompat.ActionArgumentSetTextCharsequence, text);
                bool status = node.PerformAction(global::Android.Views.Accessibility.Action.SetText, bundle);
                return status;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error setting text: {ex.Message}");
                return false;
            }
        }

        public bool PerformGlobalAction(int actionType)
        {
            try
            {
                return PerformGlobalAction((global::Android.AccessibilityServices.GlobalAction)actionType);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error performing global action: {ex.Message}");
                return false;
            }
        }

        public bool ClickByText(string text, bool useEquals = false)
        {
            try
            {
                var rootNode = RootInActiveWindow;
                if (rootNode != null)
                {
                    var node = FindNodeByText(rootNode, text, useEquals);
                    return node != null && ClickNode(node);
                }
                return false;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error clicking by text: {ex.Message}");
                return false;
            }
        }

        public bool ClickByResourceId(string resourceId, int index = 0, bool useNativeClick = false)
        {
            try
            {
                var rootNode = RootInActiveWindow;
                if (rootNode != null)
                {
                    var node = FindNodeByResourceId(rootNode, resourceId, index);
                    if (node != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Clicking node by resource ID: {resourceId}");
                        return ClickNode(node, useNativeClick);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Node with resource ID {resourceId} not found");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("AutomationAccessibilityService: Root node is null");
                }
                return false;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error clicking by resource ID: {ex.Message}");
                return false;
            }
        }

        public bool InputText(string text, int index = 0)
        {
            try
            {
                // Find focused input field or any editable field
                var rootNode = RootInActiveWindow;
                if (rootNode != null)
                {
                    var inputNode = FindEditableNode(rootNode, index);
                    if (inputNode != null)
                    {
                        return SetTextInNode(inputNode, text);
                    }
                }
                return false;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error inputting text: {ex.Message}");
                return false;
            }
        }

        public bool InputTextByResourceId(string resourceId, string text)
        {
            try
            {
                var rootNode = RootInActiveWindow;
                if (rootNode != null)
                {
                    var node = FindNodeByResourceId(rootNode, resourceId);
                    if (node != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Setting text '{text}' in node with resource ID: {resourceId}");
                        return SetTextInNode(node, text);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Node with resource ID {resourceId} not found for inputting text");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("AutomationAccessibilityService: Root node is null for inputting text");
                }
                return false;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error inputting text by resource ID: {ex.Message}");
                return false;
            }
        }

        public bool ScrollDown(bool useSecondView = false)
        {
            try
            {
                var rootNode = RootInActiveWindow;
                if (rootNode != null)
                {
                    var scrollableNode = FindScrollableNode(rootNode, useSecondView);
                    if (scrollableNode != null)
                    {
                        return scrollableNode.PerformAction(global::Android.Views.Accessibility.Action.ScrollForward);
                    }
                }
                return PerformGlobalAction((int)GlobalAction.Overview); // Fallback
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error scrolling down: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Perform a vertical swipe upwards to scroll the visible area.
        /// Uses DispatchGesture (API24+) and returns true if gesture was dispatched.
        /// </summary>
        public bool SwipeUp(int durationMs = 400)
        {
            try
            {
                if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.N)
                    return false; // DispatchGesture requires API 24+

                var ctx = global::Android.App.Application.Context;
                if (ctx == null || ctx.Resources == null)
                    return false;

                var metrics = ctx.Resources.DisplayMetrics;
                if (metrics == null)
                    return false;

                int width = metrics.WidthPixels;
                int height = metrics.HeightPixels;

                // Start in the lower-middle area and move to the upper-middle area
                float x = width * 0.5f;
                float startY = height * 0.78f; // near bottom
                float endY = height * 0.35f;   // upper-middle

                var path = new global::Android.Graphics.Path();
                path.MoveTo(x, startY);
                path.LineTo(x, endY);

                var stroke = new GestureDescription.StrokeDescription(path, 0, durationMs);
                var builder = new GestureDescription.Builder();
                builder.AddStroke(stroke);

                var gesture = builder.Build();
                if (gesture == null) return false;
                return DispatchGesture(gesture, null, null);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error performing swipe up gesture: {ex.Message}");
                return false;
            }
        }

        public bool SwipeDown(int durationMs = 400)
        {
            try
            {
                if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.N)
                    return false; // DispatchGesture requires API 24+

                var ctx = global::Android.App.Application.Context;
                if (ctx == null || ctx.Resources == null)
                    return false;

                var metrics = ctx.Resources.DisplayMetrics;
                if (metrics == null)
                    return false;

                int width = metrics.WidthPixels;
                int height = metrics.HeightPixels;

                // Start in the upper-middle area and move to the lower-middle area
                float x = width * 0.5f;
                float startY = height * 0.35f; // near top
                float endY = height * 0.78f;   // lower-middle

                var path = new global::Android.Graphics.Path();
                path.MoveTo(x, startY);
                path.LineTo(x, endY);

                var stroke = new GestureDescription.StrokeDescription(path, 0, durationMs);
                var builder = new GestureDescription.Builder();
                builder.AddStroke(stroke);

                var gesture = builder.Build();
                if (gesture == null) return false;
                return DispatchGesture(gesture, null, null);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error performing swipe down gesture: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Perform a right-to-left swipe gesture across the screen.
        /// Returns true if the gesture was dispatched, false otherwise.
        /// Requires API 24+.
        /// </summary>
        public bool SwipeRightToLeft(int durationMs = 300)
        {
            try
            {
                if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.N)
                    return false; // DispatchGesture requires API 24+

                // Delegate to API-24 helper to avoid referencing newer APIs on lower targets
                return SwipeRightToLeftApi24(durationMs);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error performing swipe gesture: {ex.Message}");
                return false;
            }
        }

        // Method that only runs on API 24 (Android N) and above
        private bool SwipeRightToLeftApi24(int durationMs = 300)
        {
            try
            {
                var ctx = global::Android.App.Application.Context;
                if (ctx == null || ctx.Resources == null)
                    return false;

                var metrics = ctx.Resources.DisplayMetrics;
                if (metrics == null)
                    return false;

                int width = metrics.WidthPixels;
                int height = metrics.HeightPixels;

                float startX = width * 0.9f;
                float endX = width * 0.1f;
                float y = height * 0.5f;

                var path = new global::Android.Graphics.Path();
                path.MoveTo(startX, y);
                path.LineTo(endX, y);

                var stroke = new GestureDescription.StrokeDescription(path, 0, durationMs);
                var builder = new GestureDescription.Builder();
                builder.AddStroke(stroke);

                var gesture = builder.Build();
                if (gesture == null) return false;
                return DispatchGesture(gesture, null, null);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error performing swipe gesture (API24): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Try to navigate back using ImageButton[@content-desc='Back'], text fallbacks, resource-id fallbacks, or global Back.
        /// Returns true if any back action was performed.
        /// </summary>
        public bool GoBack(int maxAttempts = 1, bool stopAtHome = true)
        {
            if (maxAttempts < 10) //not reset backing
            {
                for (int i = 0; i < maxAttempts; i++)
                {
                    if (IsAppInForeground())
                    {
                        PerformGlobalAction((int)GlobalAction.Back);
                        Thread.Sleep(500);
                    }
                }
                return true;
            }

            try
            {
                Thread.Sleep(2000); //only for final backing
                if (RootInActiveWindow == null) return false;
                //HandlePopups(); 

                bool canBreakGoBack = false;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    Thread.Sleep(1000);
                    if (RootInActiveWindow != null)
                    {
                        var tabFeed = FindNodeByResourceId(RootInActiveWindow, PokerLiveSConstants.SEARCH_BUTTON_ID);
                        if (tabFeed != null)
                        {
                            if (stopAtHome) break; //for starting operation
                            else canBreakGoBack = true; //for closing operation
                        }

                        if (IsAppInForeground())
                        {
                            PerformGlobalAction((int)GlobalAction.Back);
                            Thread.Sleep(100);
                        }
                    }
                }

                //final check to close app
                if (canBreakGoBack || !stopAtHome)
                {
                    while (IsAppInForeground())
                    {
                        PerformGlobalAction((int)GlobalAction.Back);
                        Thread.Sleep(100);
                    }
                    //launch this app package name just once
                    CheckForegroundAndLaunchApp(VConstants.POKER_LIVE_SCRAPER_PACKAGE);
                }

                return true;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error in GoBack: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Handle various onboarding popups that might appear during login
        /// Mirrors Python popup handling approach
        /// </summary>
        public void HandlePopups()
        {
            try
            {
                Thread.Sleep(3000);

                // Try close sheet accessibility action
                bool closeSheetClicked = false;
                var rootNode = RootInActiveWindow;
                if (rootNode == null) return;

                var closeSheetMainParent = FindNodeByResourceId(rootNode, "android:id/content");
                if (closeSheetMainParent != null)
                {
                    // Try to find node by accessibility ID "Close sheet" using a more exhaustive search
                    // This matches //android.view.View[@content-desc="Close sheet"] in XPath
                    Queue<AccessibilityNodeInfo> nodesToExplore = new Queue<AccessibilityNodeInfo>();
                    nodesToExplore.Enqueue(rootNode);

                    while (nodesToExplore.Count > 0 && !closeSheetClicked)
                    {
                        var currentNode = nodesToExplore.Dequeue();

                        // Check if this node has the "Close sheet" content description
                        if (currentNode != null && "Close sheet".Equals(currentNode.ContentDescription?.ToString()))
                        {
                            closeSheetClicked = ClickNode(currentNode);
                            break;
                        }

                        // Add children to queue
                        if (currentNode != null)
                        {
                            for (int i = 0; i < currentNode.ChildCount; i++)
                            {
                                var child = currentNode.GetChild(i);
                                if (child != null)
                                {
                                    nodesToExplore.Enqueue(child);
                                }
                            }
                        }
                    }

                    // Try ComposeView xpath approach as a last resort
                    if (!closeSheetClicked && rootNode != null)
                    {
                        // We can't use XPath directly, but we can try to find a similar node structure
                        // by navigating through the node tree
                        if (rootNode.ChildCount > 1)
                        {
                            var viewNode = rootNode.GetChild(1); // Second child, similar to android.view.View in XPath
                            if (viewNode != null && viewNode.ChildCount > 1)
                            {
                                var innerViewNode = viewNode.GetChild(1); // Second child, like view[2] in XPath
                                if (innerViewNode != null)
                                {
                                    closeSheetClicked = ClickNode(innerViewNode);
                                }
                            }
                        }
                    }

                    if (!closeSheetClicked)
                    {
                        //GoBack(1); //endless loop
                        closeSheetMainParent.PerformAction(global::Android.Views.Accessibility.Action.ScrollBackward);
                    }
                }

                // Try clicking "No thanks"
                bool noThanksClicked = ClickByResourceId("android:id/autofill_dialog_no");
                if (noThanksClicked) Thread.Sleep(1000);

                // Try clicking outside
                bool outsideClicked = ClickByResourceId("com.google.android.gms:id/touch_outside");
                if (outsideClicked) Thread.Sleep(1000);

                // Try cancel button
                bool cancelClicked = ClickByResourceId("com.google.android.gms:id/cancel");
                if (cancelClicked) Thread.Sleep(1000);

                // Close save password manager
                bool saveNoClicked = ClickByResourceId("android:id/autofill_save_no");
                if (saveNoClicked) Thread.Sleep(1000);

                // Try clicking Accept privacy
                bool adNonModalDialogButtonClicked = ClickByResourceId("com.linkedin.android:id/ad_non_modal_dialog_button2");
                if (adNonModalDialogButtonClicked) Thread.Sleep(1000);

                // Try closing notification permission dialog
                bool closeNotifClicked = ClickByResourceId("com.linkedin.android:id/notif_permission_close_img_btn");
                if (closeNotifClicked) Thread.Sleep(1000);

                // Try clicking Terms Update dialog
                bool termsUpdateClicked = ClickByResourceId("com.linkedin.android:id/ad_non_modal_dialog_close_button");
                if (termsUpdateClicked) Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error handling popups: {ex.Message}");
            }
            Thread.Sleep(2000);
        }


        public bool CheckForegroundAndLaunchApp(string packageName)
        {
            try
            {
                // First check if the app is already in the foreground
                if (IsAppInForeground(packageName))
                {
                    System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: App {packageName} is already in foreground");
                    return true;
                }

                var intent = global::Android.App.Application.Context?.PackageManager?.GetLaunchIntentForPackage(packageName);
                if (intent != null)
                {
                    // Add flags to ensure the app comes to foreground, even if it's running in background
                    intent.AddFlags(ActivityFlags.NewTask);
                    intent.AddFlags(ActivityFlags.ClearTop); // Bring existing activity to front
                    intent.AddFlags(ActivityFlags.SingleTop); // Don't create new instance if already exists
                    intent.AddFlags(ActivityFlags.ReorderToFront); // Bring to front of task stack

                    System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Launching {packageName} with enhanced flags");
                    global::Android.App.Application.Context?.StartActivity(intent);

                    return true;
                }
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: No launch intent found for {packageName}");
                return false;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error launching app: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a specific app is currently in the foreground
        /// </summary>
        public bool IsAppInForeground(string? packageName = null)
        {
            string[] packagesToCheck;
            if (string.IsNullOrEmpty(packageName))
            {
                packagesToCheck =
                [
                    VConstants.POKER_LIVE_APP_PACKAGE
                ];
            }
            else
            {
                packagesToCheck = [packageName];
            }

            try
            {
                var rootNode = RootInActiveWindow;
                if (rootNode != null)
                {
                    string? currentPackage = rootNode.PackageName?.ToString();
                    // Special case for credential manager
                    if (currentPackage == VConstants.CREDENTIAL_MANAGER_PACKAGE) return true;

                    bool result = false;
                    foreach (var pkg in packagesToCheck)
                    {
                        if (currentPackage != null && currentPackage.Contains(pkg))
                        {
                            result = true;
                            break;
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"IsAppInForeground check: current={currentPackage}, target={packageName}, result={result}");
                    return result;
                }
                System.Diagnostics.Debug.WriteLine($"IsAppInForeground check: RootInActiveWindow is null");
                return false;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error checking foreground app: {ex.Message}");
                return false;
            }
        }

        private AccessibilityNodeInfo? FindEditableNode(AccessibilityNodeInfo node, int index)
        {
            int currentCount = 0;
            return FindEditableNodeRecursive(node, index, ref currentCount);
        }

        private AccessibilityNodeInfo? FindEditableNodeRecursive(AccessibilityNodeInfo node, int targetIndex, ref int currentCount)
        {
            string nodeClass = node.ClassName?.ToString() ?? "";

            // Only match nodes with className "android.widget.EditText" or "android.widget.AutoCompleteTextView"
            if (nodeClass.Equals("android.widget.EditText", StringComparison.OrdinalIgnoreCase) ||
                nodeClass.Equals("android.widget.AutoCompleteTextView", StringComparison.OrdinalIgnoreCase))
            {
                if (currentCount == targetIndex)
                    return node;
                currentCount++;
            }

            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.GetChild(i);
                if (child != null)
                {
                    var result = FindEditableNodeRecursive(child, targetIndex, ref currentCount);
                    if (result != null) return result;
                }
            }

            return null;
        }

        private AccessibilityNodeInfo? FindScrollableNode(AccessibilityNodeInfo node, bool useSecondView = false)
        {
            // Fast path: when consumer only wants the first scrollable node
            if (!useSecondView)
            {
                if (node.Scrollable)
                    return node;

                for (int i = 0; i < node.ChildCount; i++)
                {
                    var child = node.GetChild(i);
                    if (child == null) continue;
                    var res = FindScrollableNode(child, false);
                    if (res != null) return res;
                }

                return null;
            }

            // If caller requests the second scrollable view, perform a DFS with a running counter
            int counter = 0;
            return FindScrollableNodeByIndex(node, 2, ref counter);
        }

        // Helper that finds the Nth scrollable node (1-based index) using depth-first traversal
        private AccessibilityNodeInfo? FindScrollableNodeByIndex(AccessibilityNodeInfo? node, int targetIndex, ref int counter)
        {
            if (node == null) return null;

            try
            {
                if (node.Scrollable)
                {
                    counter++;
                    if (counter == targetIndex) return node;
                }

                for (int i = 0; i < node.ChildCount; i++)
                {
                    var child = node.GetChild(i);
                    if (child == null) continue;
                    var res = FindScrollableNodeByIndex(child, targetIndex, ref counter);
                    if (res != null) return res;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error in FindScrollableNodeByIndex: {ex.Message}");
            }

            return null;
        }

        public AccessibilityNodeInfo? FindNodeByText(AccessibilityNodeInfo? node, string text, bool useEquals = false)
        {
            if (node == null)
                return node;

            if (useEquals)
            {
                if (node.Text?.ToString()?.Trim().Equals(text.Trim()) == true ||
                    node.ContentDescription?.ToString()?.Trim().Equals(text.Trim()) == true)
                {
                    return node;
                }
            }
            else
            {
                if (node.Text?.ToString()?.Contains(text) == true ||
                    node.ContentDescription?.ToString()?.Contains(text) == true)
                {
                    return node;
                }
            }

            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.GetChild(i);
                if (child != null)
                {
                    var result = FindNodeByText(child, text, useEquals);
                    if (result != null) return result;
                }
            }

            return null;
        }

        public AccessibilityNodeInfo? FindNodeByResourceId(AccessibilityNodeInfo? nodeSent, string resourceId, int index = 0)
        {
            var node = nodeSent ?? RootInActiveWindow;
            var result = new List<AccessibilityNodeInfo>();
            if (node == null)
                return node;

            var nodes = node.FindAccessibilityNodeInfosByViewId(resourceId);
            if (nodes != null)
                result.AddRange(nodes);

            return result.ElementAtOrDefault(index);
        }
        public List<AccessibilityNodeInfo>? FindNodesByResourceId(string resourceId)
        {
            if (RootInActiveWindow == null)
                return [];

            var result = new List<AccessibilityNodeInfo>();
            var nodes = RootInActiveWindow.FindAccessibilityNodeInfosByViewId(resourceId);
            if (nodes != null)
                result.AddRange(nodes);

            return result;
        }

        private AccessibilityNodeInfo? FindNodeByClassName(AccessibilityNodeInfo node, string className)
        {
            if (node.ClassName?.Contains(className) == true)
                return node;

            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.GetChild(i);
                if (child != null)
                {
                    var result = FindNodeByClassName(child, className);
                    if (result != null) return result;
                }
            }

            return null;
        }

        public List<AccessibilityNodeInfo> FindNodesByClassName(string className, AccessibilityNodeInfo? nodeSent = null)
        {
            var results = new List<AccessibilityNodeInfo>();
            AccessibilityNodeInfo? node = nodeSent ?? RootInActiveWindow;
            if (node == null) return results;

            try
            {
                if (node == null || string.IsNullOrEmpty(className))
                    return results;

                if (node.ClassName?.Contains(className) == true)
                    results.Add(node);

                for (int i = 0; i < node.ChildCount; i++)
                {
                    var child = node.GetChild(i);
                    if (child != null)
                    {
                        var childResults = FindNodesByClassName(className, child);
                        results.AddRange(childResults);
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error in FindNodesByClassName: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Find a node by class name and exact content description (content-desc).
        /// Mirrors XPath: //android.widget.ImageButton[@content-desc="Back"]
        /// </summary>
        private AccessibilityNodeInfo? FindNodeByClassAndContentDesc(AccessibilityNodeInfo node, string className, string contentDesc)
        {
            try
            {
                if (node.ClassName?.ToString()?.Contains(className) == true &&
                    node.ContentDescription?.ToString() == contentDesc)
                {
                    return node;
                }

                for (int i = 0; i < node.ChildCount; i++)
                {
                    var child = node.GetChild(i);
                    if (child != null)
                    {
                        var result = FindNodeByClassAndContentDesc(child, className, contentDesc);
                        if (result != null) return result;
                    }
                }

                return null;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error finding node by class+contentDesc: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the text of a node identified by its resource ID
        /// </summary>
        public string? GetTextByResourceId(string resourceId)
        {
            try
            {
                var rootNode = RootInActiveWindow;
                if (rootNode == null)
                    return null;

                var node = FindNodeByResourceId(rootNode, resourceId);
                if (node != null && node.Text != null)
                {
                    return node.Text.ToString();
                }

                // Check for ContentDescription if Text is null
                if (node != null && node.ContentDescription != null)
                {
                    return node.ContentDescription.ToString();
                }

                return null;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error getting text by resource ID: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all text values from nodes matching a specific resource ID
        /// </summary>
        public List<string> GetAllTextsByResourceId(string resourceId)
        {
            var results = new List<string>();
            try
            {
                var rootNode = RootInActiveWindow;
                if (rootNode == null)
                    return results;

                FindAllTextsByResourceId(rootNode, resourceId, results);
                return results;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error getting all texts by resource ID: {ex.Message}");
                return results;
            }
        }

        /// <summary>
        /// Helper method to find all texts by resource ID
        /// </summary>
        private void FindAllTextsByResourceId(AccessibilityNodeInfo node, string resourceId, List<string> results)
        {
            // First try to find directly by the API
            var foundNodes = node.FindAccessibilityNodeInfosByViewId(resourceId);
            if (foundNodes != null && foundNodes.Count > 0)
            {
                foreach (var foundNode in foundNodes)
                {
                    if (foundNode.Text != null)
                    {
                        results.Add(foundNode.Text.ToString());
                    }
                }
                return;
            }

            // Fallback to checking ViewIdResourceName
            if (node.ViewIdResourceName?.Contains(resourceId) == true && node.Text != null)
            {
                results.Add(node.Text.ToString());
            }

            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.GetChild(i);
                if (child != null)
                {
                    FindAllTextsByResourceId(child, resourceId, results);
                }
            }
        }

        private void ProcessQueuedActions()
        {
            while (_actionQueue.TryDequeue(out var action))
            {
                try
                {
                    // Process automation actions here if needed
                    System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Processing action: {action.Type}");
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error processing action: {ex.Message}");
                }
            }
        }
        private AccessibilityNodeInfo? FindFocusedNode(AccessibilityNodeInfo node)
        {
            if (node.Focused)
                return node;

            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.GetChild(i);
                if (child != null)
                {
                    var result = FindFocusedNode(child);
                    if (result != null) return result;
                }
            }

            return null;
        }

        internal IList<AccessibilityNodeInfo>? GetViewsByClassAndResourceID(AccessibilityNodeInfo? root, string className, string resourceId)
        {
            var results = new List<AccessibilityNodeInfo>();

            try
            {
                if (root == null) return results;
                var allNodes = root.FindAccessibilityNodeInfosByViewId(resourceId);
                if (allNodes != null)
                {
                    AccessibilityNodeInfo? oldNode = null;
                    foreach (var node in allNodes)
                    {
                        string classNameNode = node.ClassName?.ToString() ?? "";
                        if (classNameNode.Equals(className, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(oldNode ?? node);
                        }
                        else oldNode = node;
                    }
                }

                return results;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error in GetViewsByClassAndResourceID: {ex.Message}");
                return results;
            }
        }

        internal void ClickScreenHorizontalEnd()
        {
            try
            {
                var ctx = global::Android.App.Application.Context;
                if (ctx == null || ctx.Resources == null)
                    return;

                var metrics = ctx.Resources.DisplayMetrics;
                if (metrics == null)
                    return;

                int width = metrics.WidthPixels;
                int height = metrics.HeightPixels;

                float y = height * 0.2f; // Upper portion of the screen
                float x = width * 0.9f; // Near right edge

                var path = new global::Android.Graphics.Path();
                path.MoveTo(x, y);

                var stroke = new GestureDescription.StrokeDescription(path, 0, 100);
                var builder = new GestureDescription.Builder();
                builder.AddStroke(stroke);

                var gesture = builder.Build();
                if (gesture == null) return;
                DispatchGesture(gesture, null, null);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error performing horizontal end click gesture: {ex.Message}");
            }
        }

        internal void SaveCurrentViewSource()
        {
            try
            {
                var rootNode = RootInActiveWindow;
                if (rootNode == null)
                {
                    System.Diagnostics.Debug.WriteLine("AutomationAccessibilityService: Root node is null, cannot save view source");
                    return;
                }

                string viewSource = GetViewSourceRecursive(rootNode, 0);

                // Save to Android's external storage (accessible via file manager/ADB)
                string fileName = $"viewsource_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath;

                // Try to save to external storage first (accessible from laptop)
                var externalStorageDir = global::Android.OS.Environment.GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDownloads);
                if (externalStorageDir != null && externalStorageDir.Exists())
                {
                    filePath = System.IO.Path.Combine(externalStorageDir.AbsolutePath, fileName);
                }
                else
                {
                    // Fallback to app's external files directory
                    var appExternalDir = global::Android.App.Application.Context?.GetExternalFilesDir(null);
                    filePath = System.IO.Path.Combine(appExternalDir?.AbsolutePath ?? "", fileName);
                }

                System.IO.File.WriteAllText(filePath, viewSource);
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: View source saved to: {filePath}");
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: You can access this file via ADB: adb pull \"{filePath}\" ~/Desktop/");

                // Also log the view source to debug output for immediate viewing
                System.Diagnostics.Debug.WriteLine("=== VIEW SOURCE START ===");
                System.Diagnostics.Debug.WriteLine(viewSource);
                System.Diagnostics.Debug.WriteLine("=== VIEW SOURCE END ===");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error saving view source: {ex.Message}");

                // Fallback to internal storage and debug output
                try
                {
                    var rootNode = RootInActiveWindow;
                    if (rootNode != null)
                    {
                        string viewSource = GetViewSourceRecursive(rootNode, 0);
                        string fallbackPath = System.IO.Path.Combine(
                            global::Android.App.Application.Context?.FilesDir?.AbsolutePath ?? "",
                            $"viewsource_fallback_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                        System.IO.File.WriteAllText(fallbackPath, viewSource);
                        System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Fallback - View source saved to: {fallbackPath}");

                        // Log to debug output as well
                        System.Diagnostics.Debug.WriteLine("=== VIEW SOURCE START (FALLBACK) ===");
                        System.Diagnostics.Debug.WriteLine(viewSource);
                        System.Diagnostics.Debug.WriteLine("=== VIEW SOURCE END (FALLBACK) ===");
                    }
                }
                catch (System.Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Fallback save also failed: {fallbackEx.Message}");
                }
            }
        }

        private string GetViewSourceRecursive(AccessibilityNodeInfo node, int depth)
        {
            try
            {
                string indent = new string(' ', depth * 2);
                string nodeInfo = $"{indent}Class: {node.ClassName}, Text: {node.Text}, ContentDesc: {node.ContentDescription}, ResourceId: {node.ViewIdResourceName}, ChildCount: {node.ChildCount}\n";

                for (int i = 0; i < node.ChildCount; i++)
                {
                    var child = node.GetChild(i);
                    if (child != null)
                    {
                        nodeInfo += GetViewSourceRecursive(child, depth + 1);
                    }
                }

                return nodeInfo;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error in GetViewSourceRecursive: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Finds and clicks a button in the top-right corner of the screen.
        /// Useful for "Post", "Done", "Save", "Send" buttons commonly placed there.
        /// </summary>
        public bool ClickTopRightCornerButton()
        {
            try
            {
                var rootNode = RootInActiveWindow;
                if (rootNode == null)
                {
                    System.Diagnostics.Debug.WriteLine("AutomationAccessibilityService: No root node available");
                    return false;
                }

                // Get screen dimensions (approximate)
                var displayMetrics = Resources?.DisplayMetrics;
                if (displayMetrics == null)
                {
                    System.Diagnostics.Debug.WriteLine("AutomationAccessibilityService: Could not get display metrics");
                    return false;
                }

                int screenWidth = displayMetrics.WidthPixels;
                int screenHeight = displayMetrics.HeightPixels;

                // Define top-right corner area: right 25% of screen, top 15% of screen
                int rightThreshold = (int)(screenWidth * 0.75);
                int topThreshold = (int)(screenHeight * 0.15);

                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Screen dimensions: {screenWidth}x{screenHeight}");
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Looking for buttons in top-right corner (X > {rightThreshold}, Y < {topThreshold})");

                // Find all clickable nodes
                var clickableNodes = new List<AccessibilityNodeInfo>();
                FindClickableNodes(rootNode, clickableNodes);

                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Found {clickableNodes.Count} clickable nodes");

                // Filter nodes in top-right corner and get their positions
                var topRightButtons = new List<(AccessibilityNodeInfo node, int x, int y)>();
                foreach (var node in clickableNodes)
                {
                    var rect = new global::Android.Graphics.Rect();
                    node.GetBoundsInScreen(rect);

                    // Check if node is in top-right corner
                    int centerX = (rect.Left + rect.Right) / 2;
                    int centerY = (rect.Top + rect.Bottom) / 2;

                    if (centerX > rightThreshold && centerY < topThreshold)
                    {
                        topRightButtons.Add((node, centerX, centerY));
                        System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Found top-right button at ({centerX}, {centerY}), " +
                            $"Text: '{node.Text}', ContentDesc: '{node.ContentDescription}', ClassName: '{node.ClassName}'");
                    }
                }

                if (topRightButtons.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("AutomationAccessibilityService: No buttons found in top-right corner");
                    return false;
                }

                // Sort by position: rightmost first, then topmost
                topRightButtons.Sort((a, b) =>
                {
                    // First sort by X (rightmost first)
                    int xCompare = b.x.CompareTo(a.x);
                    if (xCompare != 0) return xCompare;
                    // Then by Y (topmost first)
                    return a.y.CompareTo(b.y);
                });

                // Try to click the rightmost-topmost button
                var targetButton = topRightButtons[0];
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Attempting to click button at ({targetButton.x}, {targetButton.y})");

                bool clicked = ClickNode(targetButton.node);
                if (clicked)
                {
                    System.Diagnostics.Debug.WriteLine("AutomationAccessibilityService: Successfully clicked top-right corner button");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("AutomationAccessibilityService: Failed to click top-right corner button");
                }

                return clicked;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error in ClickTopRightCornerButton: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Recursively finds all clickable nodes in the view hierarchy
        /// </summary>
        private void FindClickableNodes(AccessibilityNodeInfo node, List<AccessibilityNodeInfo> results)
        {
            try
            {
                if (node == null) return;

                // Check if node is clickable and visible
                if (node.Clickable && node.VisibleToUser)
                {
                    // Prioritize buttons and common clickable elements
                    string? className = node.ClassName?.ToString();
                    if (className != null && (
                        className.Contains("Button") ||
                        className.Contains("ImageView") ||
                        className.Contains("TextView")))
                    {
                        results.Add(node);
                    }
                }

                // Recurse through children
                for (int i = 0; i < node.ChildCount; i++)
                {
                    var child = node.GetChild(i);
                    if (child != null)
                    {
                        FindClickableNodes(child, results);
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutomationAccessibilityService: Error in FindClickableNodes: {ex.Message}");
            }
        }

        /// <summary>
        /// Filter nodes to only include visible ones (workaround for tab switching issue)
        /// When tabs are switched, RootInActiveWindow may contain nodes from all tabs.
        /// This filters to only get nodes that are actually visible on screen.
        /// </summary>
        public List<AccessibilityNodeInfo> FilterVisibleNodes(List<AccessibilityNodeInfo>? nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return new List<AccessibilityNodeInfo>();

            var visibleNodes = new List<AccessibilityNodeInfo>();

            try
            {
                // Get screen bounds to filter nodes that are actually on screen
                var rootNode = RootInActiveWindow;
                if (rootNode == null)
                    return nodes; // Fallback: return all if we can't check visibility

                var screenBounds = new global::Android.Graphics.Rect();
                rootNode.GetBoundsInScreen(screenBounds);

                foreach (var node in nodes)
                {
                    try
                    {
                        // Check if node is visible to user
                        if (!node.VisibleToUser)
                            continue;

                        // Check if node bounds are within screen bounds
                        var nodeBounds = new global::Android.Graphics.Rect();
                        node.GetBoundsInScreen(nodeBounds);

                        // Check if node is actually visible on screen (has valid bounds and intersects with screen)
                        int nodeWidth = nodeBounds.Right - nodeBounds.Left;
                        int nodeHeight = nodeBounds.Bottom - nodeBounds.Top;

                        if (nodeWidth > 0 && nodeHeight > 0)
                        {
                            // Check if node is within reasonable screen bounds (with some tolerance)
                            // Sometimes nodes from hidden tabs might have bounds but be off-screen
                            if (nodeBounds.Top >= screenBounds.Top - 100 &&
                                nodeBounds.Bottom <= screenBounds.Bottom + 100 &&
                                nodeBounds.Left >= screenBounds.Left - 100 &&
                                nodeBounds.Right <= screenBounds.Right + 100)
                            {
                                visibleNodes.Add(node);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // If we can't check bounds, skip this node
                        System.Diagnostics.Debug.WriteLine($"PokerLiveAutomation: Error checking node visibility: {ex.Message}");
                        continue;
                    }
                }

                if (visibleNodes.Count > 0)
                {
                    // Sort visible nodes by Y position (top to bottom) to ensure consistent order
                    // This helps when multiple tabs have overlapping content
                    visibleNodes.Sort((a, b) =>
                    {
                        try
                        {
                            var boundsA = new global::Android.Graphics.Rect();
                            var boundsB = new global::Android.Graphics.Rect();
                            a.GetBoundsInScreen(boundsA);
                            b.GetBoundsInScreen(boundsB);

                            // Sort by top position first, then by left position
                            int yCompare = boundsA.Top.CompareTo(boundsB.Top);
                            if (yCompare != 0) return yCompare;
                            return boundsA.Left.CompareTo(boundsB.Left);
                        }
                        catch
                        {
                            return 0;
                        }
                    });
                }
                return visibleNodes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PokerLiveAutomation: Error filtering visible nodes: {ex.Message}");
                return nodes ?? new List<AccessibilityNodeInfo>();
            }
        }
    }

    public class AutomationAction
    {
        public string Type { get; set; } = "";
    }

    public class AutomationStatus
    {
        public bool IsRunning { get; set; }
        public int WorkerCount { get; set; }
        public int RunningWorkers { get; set; }
        public int ConfiguredAccounts { get; set; }
    }

    public enum GlobalAction
    {
        Back = 1,
        Home = 2,
        Overview = 3,
        Notifications = 4,
        QuickSettings = 5
    }
}
