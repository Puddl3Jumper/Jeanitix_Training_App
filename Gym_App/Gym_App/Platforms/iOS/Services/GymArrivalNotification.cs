using Foundation;
using UserNotifications;

namespace Gym_App.Services
{
    /// <summary>
    /// Shows a local push notification when the user arrives at the gym on iOS.
    /// Mirrors the Android GymArrivalNotification (NotificationCompat) in
    /// Platforms/Android/Services/GymArrivalNotification.cs.
    /// </summary>
    internal static class GymArrivalNotification
    {
        private const string CategoryIdentifier = "gym_arrival";
        private const string NotificationIdentifier = "jeanetix_gym_arrival";

        /// <summary>
        /// Posts an immediate local notification announcing gym arrival.
        /// </summary>
        /// <param name="displayFirstName">First name to use in the message, or "there" as a fallback.</param>
        public static void Show(string displayFirstName)
        {
            UNUserNotificationCenter.Current.RequestAuthorization(
                UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge,
                (granted, error) =>
                {
                    if (!granted)
                        return;

                    DeliverNotification(displayFirstName);
                });
        }

        private static void DeliverNotification(string displayFirstName)
        {
            var message = string.IsNullOrWhiteSpace(displayFirstName) || displayFirstName == "there"
                ? "You are here 🏋️"
                : $"Hi {displayFirstName}, You are here 🏋️";

            var content = new UNMutableNotificationContent
            {
                Title = "Jeanetix",
                Body  = message,
                Sound = UNNotificationSound.Default
            };

            // Deliver immediately (nil trigger = now).
            var request = UNNotificationRequest.FromIdentifier(
                NotificationIdentifier,
                content,
                trigger: null);

            UNUserNotificationCenter.Current.AddNotificationRequest(request, error =>
            {
                if (error != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[GymArrivalNotification] Failed to post notification: {error.LocalizedDescription}");
                }
            });
        }
    }
}
