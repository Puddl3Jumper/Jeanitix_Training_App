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

        private const string PrefsName = "user_profile";
        private const string ThemeModeKey = "theme_mode";

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

        public static void ApplyTheme(Activity activity)
        {
            var mode = GetSavedThemeMode(activity);

            if (mode == ThemeModeLight)
            {
                activity.SetTheme(Resource.Style.Theme_GymApp_Light);
                return;
            }

            if (mode == ThemeModeDark)
            {
                activity.SetTheme(Resource.Style.Theme_GymApp);
                return;
            }

            var uiMode = activity.Resources?.Configuration?.UiMode ?? UiMode.TypeUndefined;
            var isNight = (uiMode & UiMode.NightMask) == UiMode.NightYes;
            activity.SetTheme(isNight ? Resource.Style.Theme_GymApp : Resource.Style.Theme_GymApp_Light);
        }
    }
}
