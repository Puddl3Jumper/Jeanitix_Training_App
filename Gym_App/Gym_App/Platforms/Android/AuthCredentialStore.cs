using Android.Content;
using System.Security.Cryptography;
using System.Text;

namespace Gym_App.Data
{
    public static class AuthCredentialStore
    {
        private const string AccountsPrefsName = "user_accounts";

        public static bool AccountExists(Context context, string email)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
                return false;

            var prefs = context.GetSharedPreferences(AccountsPrefsName, FileCreationMode.Private);
            var storedHash = prefs?.GetString(BuildPasswordKey(normalizedEmail), null);
            return !string.IsNullOrWhiteSpace(storedHash);
        }

        public static void UpsertAccount(Context context, string email, string password)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
                return;

            var prefs = context.GetSharedPreferences(AccountsPrefsName, FileCreationMode.Private);
            prefs?.Edit()
                ?.PutString(BuildPasswordKey(normalizedEmail), HashPassword(password))
                ?.Apply();
        }

        public static bool ValidateCredentials(Context context, string email, string password)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
                return false;

            var prefs = context.GetSharedPreferences(AccountsPrefsName, FileCreationMode.Private);
            var storedHash = prefs?.GetString(BuildPasswordKey(normalizedEmail), null);
            if (string.IsNullOrWhiteSpace(storedHash))
                return false;

            return string.Equals(storedHash, HashPassword(password), StringComparison.Ordinal);
        }

        public static bool ResetPassword(Context context, string email, string newPassword)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            var prefs = context.GetSharedPreferences(AccountsPrefsName, FileCreationMode.Private);
            var key = BuildPasswordKey(normalizedEmail);
            var storedHash = prefs?.GetString(key, null);
            if (string.IsNullOrWhiteSpace(storedHash))
                return false;

            prefs?.Edit()
                ?.PutString(key, HashPassword(newPassword))
                ?.Apply();

            return true;
        }

        private static string NormalizeEmail(string email)
        {
            return string.IsNullOrWhiteSpace(email)
                ? string.Empty
                : email.Trim().ToLowerInvariant();
        }

        private static string BuildPasswordKey(string normalizedEmail)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail));
            return $"pwd_{Convert.ToHexString(bytes)}";
        }

        private static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }
    }
}