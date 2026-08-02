using Foundation;
using System.Security.Cryptography;
using System.Text;

namespace Gym_App.Data
{
    /// <summary>
    /// Stores hashed local-account credentials in NSUserDefaults.
    /// For an account that uses Firebase cloud auth the password is never stored here;
    /// this class is only used for the offline/local account mode.
    /// </summary>
    public static class AuthCredentialStore
    {
        // Keys are prefixed with the prefs-group name to mirror Android SharedPreferences namespacing.
        private const string KeyPrefix = "user_accounts.";

        public static bool AccountExists(string email)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
                return false;

            var key = KeyPrefix + BuildPasswordKey(normalizedEmail);
            return NSUserDefaults.StandardUserDefaults.StringForKey(key) != null;
        }

        public static void UpsertAccount(string email, string password)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
                return;

            var key = KeyPrefix + BuildPasswordKey(normalizedEmail);
            NSUserDefaults.StandardUserDefaults.SetString(HashPassword(password), key);
            NSUserDefaults.StandardUserDefaults.Synchronize();
        }

        public static bool ValidateCredentials(string email, string password)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
                return false;

            var key = KeyPrefix + BuildPasswordKey(normalizedEmail);
            var storedHash = NSUserDefaults.StandardUserDefaults.StringForKey(key);
            if (string.IsNullOrWhiteSpace(storedHash))
                return false;

            return string.Equals(storedHash, HashPassword(password), StringComparison.Ordinal);
        }

        public static bool ResetPassword(string email, string newPassword)
        {
            var normalizedEmail = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            var key = KeyPrefix + BuildPasswordKey(normalizedEmail);
            var storedHash = NSUserDefaults.StandardUserDefaults.StringForKey(key);
            if (string.IsNullOrWhiteSpace(storedHash))
                return false;

            NSUserDefaults.StandardUserDefaults.SetString(HashPassword(newPassword), key);
            NSUserDefaults.StandardUserDefaults.Synchronize();
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
