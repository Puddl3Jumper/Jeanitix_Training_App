using Android.App;
using Android.Content;
using Android.Content.Res;

namespace Gym_App
{
    public static class ThemeManager
    {
        public const string ThemeModeLight = "light";
        public const string ThemeModeDark = "dark";
        public const string ThemeModeSystem = "system";

        public const string FontModeSans = "sans";
        public const string FontModeSerif = "serif";
        public const string FontModeMono = "mono";

        private const string PrefsName = "user_profile";
        private const string ThemeModeKey = "theme_mode";
        private const string FontModeKey = "font_mode";

        public static string GetSavedThemeMode(Context context)
        {
            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            return prefs?.GetString(ThemeModeKey, ThemeModeSystem) ?? ThemeModeSystem;
        }

        public static void SaveThemeMode(Context context, string mode)
        {
            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            prefs?.Edit()?.PutString(ThemeModeKey, mode)?.Apply();
        }

        public static string GetSavedFontMode(Context context)
        {
            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            return prefs?.GetString(FontModeKey, FontModeSans) ?? FontModeSans;
        }

        public static void SaveFontMode(Context context, string mode)
        {
            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            prefs?.Edit()?.PutString(FontModeKey, mode)?.Apply();
        }

        public static void ApplyTheme(Activity activity)
        {
            var mode = GetSavedThemeMode(activity);
            var fontMode = GetSavedFontMode(activity);

            bool useLight;
            if (mode == ThemeModeLight)
            {
                useLight = true;
            }
            else if (mode == ThemeModeDark)
            {
                useLight = false;
            }
            else
            {
                var uiMode = activity.Resources?.Configuration?.UiMode ?? UiMode.TypeUndefined;
                var isNight = (uiMode & UiMode.NightMask) == UiMode.NightYes;
                useLight = !isNight;
            }

            int themeRes = useLight ? Resource.Style.Theme_GymApp_Light : Resource.Style.Theme_GymApp;

            if (fontMode == FontModeSerif)
            {
                themeRes = useLight ? Resource.Style.Theme_GymApp_Light_Serif : Resource.Style.Theme_GymApp_Serif;
            }
            else if (fontMode == FontModeMono)
            {
                themeRes = useLight ? Resource.Style.Theme_GymApp_Light_Mono : Resource.Style.Theme_GymApp_Mono;
            }

            activity.SetTheme(themeRes);
        }
    }
}
