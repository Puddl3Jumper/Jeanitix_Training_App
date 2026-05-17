using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Gym_App.Activities;

namespace Gym_App.Services
{
    internal static class GymArrivalNotification
    {
        private const string ChannelId = "gym_arrival";
        private const int NotificationId = 41001;

        public static void Show(Context context, string displayFirstName)
        {
            EnsureChannel(context);

            var openIntent = new Intent(context, typeof(HomeActivity));
            openIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NewTask);
            var pendingFlags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
            var contentIntent = PendingIntent.GetActivity(context, 0, openIntent, pendingFlags);

            var message = string.IsNullOrWhiteSpace(displayFirstName) || displayFirstName == "there"
                ? "You are here"
                : $"Hi {displayFirstName}, You are here";

            var builder = new NotificationCompat.Builder(context, ChannelId)
                .SetSmallIcon(Resource.Mipmap.ic_launcher)
                .SetContentTitle(context.GetString(Resource.String.app_name))
                .SetContentText(message)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(message))
                .SetPriority((int)NotificationPriority.High)
                .SetAutoCancel(true)
                .SetContentIntent(contentIntent);

            NotificationManagerCompat.From(context).Notify(NotificationId, builder.Build());
        }

        private static void EnsureChannel(Context context)
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26))
                return;

            var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
            if (manager?.GetNotificationChannel(ChannelId) != null)
                return;

            var channel = new NotificationChannel(
                ChannelId,
                context.GetString(Resource.String.gym_arrival_channel_name),
                NotificationImportance.High)
            {
                Description = context.GetString(Resource.String.gym_arrival_channel_description)
            };
            manager?.CreateNotificationChannel(channel);
        }
    }
}
