using Foundation;
using UIKit;

namespace Gym_App
{
    /// <summary>
    /// Manages app theme (light/dark/system) and font style on iOS.
    /// Settings are persisted in NSUserDefaults.
    /// Mirrors the Android ThemeManager in Platforms/Android/ThemeManager.cs.
    /// </summary>
    public static class ThemeManager
    {
        public const string ThemeModeLight  = "light";
        public const string ThemeModeDark   = "dark";
        public const string ThemeModeSystem = "system";

        public const string FontModeSans  = "sans";
        public const string FontModeSerif = "serif";
        public const string FontModeMono  = "mono";

        // Keys are prefixed with the prefs-group name to mirror Android SharedPreferences namespacing.
        private const string ThemeModeKey = "user_profile.theme_mode";
        private const string FontModeKey  = "user_profile.font_mode";

        public static string GetSavedThemeMode()
        {
            return NSUserDefaults.StandardUserDefaults.StringForKey(ThemeModeKey) ?? ThemeModeSystem;
        }

        public static void SaveThemeMode(string mode)
        {
            NSUserDefaults.StandardUserDefaults.SetString(mode, ThemeModeKey);
        }

        public static string GetSavedFontMode()
        {
            return NSUserDefaults.StandardUserDefaults.StringForKey(FontModeKey) ?? FontModeSans;
        }

        public static void SaveFontMode(string mode)
        {
            NSUserDefaults.StandardUserDefaults.SetString(mode, FontModeKey);
        }

        /// <summary>
        /// Applies the saved theme to the provided view controller and its window.
        /// Call from <c>ViewDidLoad</c> or <c>ViewWillAppear</c>.
        /// </summary>
        public static void ApplyTheme(UIViewController viewController)
        {
            if (!OperatingSystem.IsIOSVersionAtLeast(13))
                return;

            var style = ResolveStyle(GetSavedThemeMode());
            viewController.OverrideUserInterfaceStyle = style;
        }

        /// <summary>
        /// Applies the saved theme directly to a <see cref="UIWindow"/>.
        /// Call from <c>AppDelegate.FinishedLaunching</c> after the window is created.
        /// </summary>
        public static void ApplyThemeToWindow(UIWindow? window)
        {
            if (window == null || !OperatingSystem.IsIOSVersionAtLeast(13))
                return;

            window.OverrideUserInterfaceStyle = ResolveStyle(GetSavedThemeMode());
        }

        private static UIUserInterfaceStyle ResolveStyle(string mode) => mode switch
        {
            ThemeModeLight => UIUserInterfaceStyle.Light,
            ThemeModeDark  => UIUserInterfaceStyle.Dark,
            _              => UIUserInterfaceStyle.Unspecified
        };
    }
}
