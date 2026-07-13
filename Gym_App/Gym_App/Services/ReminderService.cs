using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Speech.Tts;
using Gym_App.Data;
using Java.Util;

namespace Gym_App.Services
{
    [Service(Exported = false)]
    public class ReminderService : Service, TextToSpeech.IOnInitListener
    {
        private TextToSpeech? _tts;
        private MediaPlayer? _mediaPlayer;
        private string _message = string.Empty;

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            var db = new GymDatabase();
            var groups = db.GetDailyWorkoutGroups();
            var rotation = $"{groups[0]}, {groups[1]} and {groups[2]}";
            _message = $"Today is {rotation}. Get your ass off and start your workout today!";

            // Play the reminder chime first
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
                // Stop the service once speech is queued — TTS engine continues asynchronously
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
    }
}
