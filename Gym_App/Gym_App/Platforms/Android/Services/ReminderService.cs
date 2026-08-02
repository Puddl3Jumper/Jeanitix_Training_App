using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Speech.Tts;
using Gym_App.Activities;
using Gym_App.Data;
using Java.Util;

namespace Gym_App.Services
{
    [Service(Exported = false)]
    public class ReminderService : Service, TextToSpeech.IOnInitListener
    {
        private const int NotificationId = 1001;
        private const string ChannelId = "reminder_channel_v2";

        private TextToSpeech? _tts;
        private MediaPlayer? _mediaPlayer;
        private string _message = string.Empty;

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            var isPreview = intent?.GetBooleanExtra("preview", false) ?? false;

            // Only promote to foreground service when started from the alarm receiver
            // (background context). For the in-app preview the app is already foreground.
            if (!isPreview)
            {
                EnsureNotificationChannel();
                var notification = BuildNotification("Today's workout rotation is ready 💪");
                if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake) // API 34
                    StartForeground(NotificationId, notification, (Android.Content.PM.ForegroundService)0x800);
                else
                    StartForeground(NotificationId, notification);
            }

            var db = new GymDatabase();
            var groups = db.GetDailyWorkoutGroups();
            var rotation = $"{groups[0]}, {groups[1]} and {groups[2]}";
            _message = $"Today is {rotation}. Get your ass off and start your workout today!";

            // Update notification with the actual rotation text
            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.Notify(NotificationId, BuildNotification($"Today: {rotation}"));

            // Play the reminder chime
            try
            {
                _mediaPlayer = MediaPlayer.Create(this, Resource.Raw.reminder_sound);
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Completion += (s, e) =>
                    {
                        _mediaPlayer?.Release();
                        _mediaPlayer = null;
                    };
                    _mediaPlayer.Start();
                }
            }
            catch
            {
                // Gracefully skip sound if file is missing or unplayable
            }

            // Then speak the daily workout message via TTS
            _tts = new TextToSpeech(this, this);
            return StartCommandResult.NotSticky;
        }

        public void OnInit(OperationResult status)
        {
            if (status == OperationResult.Success && _tts != null)
            {
                _tts.SetLanguage(Locale.Us);
                _tts.Speak(_message, QueueMode.Flush, null, "reminder");
                Task.Delay(TimeSpan.FromSeconds(8)).ContinueWith(_ => StopSelf());
            }
            else
            {
                StopSelf();
            }
        }

        public override void OnDestroy()
        {
            _mediaPlayer?.Stop();
            _mediaPlayer?.Release();
            _mediaPlayer = null;
            _tts?.Stop();
            _tts?.Shutdown();
            _tts = null;
            base.OnDestroy();
        }

        private void EnsureNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                return;

            var manager = (NotificationManager?)GetSystemService(NotificationService);
            if (manager?.GetNotificationChannel(ChannelId) != null)
                return;

            var channel = new NotificationChannel(ChannelId, "Workout Reminders", NotificationImportance.High)
            {
                Description = "Daily workout reminder"
            };
            channel.EnableVibration(true);
            manager?.CreateNotificationChannel(channel);
        }

        private Notification BuildNotification(string body)
        {
            var tapIntent = new Intent(this, typeof(HomeActivity));
            tapIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            var pendingIntent = PendingIntent.GetActivity(
                this, 0, tapIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            return new Notification.Builder(this, ChannelId)
                .SetSmallIcon(Resource.Mipmap.ic_launcher)
                .SetContentTitle("Jeanetix Reminder 🏋️")
                .SetContentText(body)
                .SetStyle(new Notification.BigTextStyle().BigText(body))
                .SetContentIntent(pendingIntent)
                .SetAutoCancel(true)
                .SetPriority((int)NotificationPriority.High)
                .Build();
        }
    }
}
