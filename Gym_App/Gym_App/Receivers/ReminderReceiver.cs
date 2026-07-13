using Android.App;
using Android.Content;
using Gym_App.Services;

namespace Gym_App.Receivers
{
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter(new[] { "com.leiyu.GymJournal.ACTION_DAILY_REMINDER" })]
    public class ReminderReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context == null)
                return;

            var serviceIntent = new Intent(context, typeof(ReminderService));
            context.StartForegroundService(serviceIntent);
        }
    }
}
