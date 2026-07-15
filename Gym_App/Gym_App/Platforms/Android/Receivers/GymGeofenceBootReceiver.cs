using Android.App;
using Android.Content;
using Gym_App.Services;

namespace Gym_App.Receivers
{
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter(new[] { Intent.ActionBootCompleted })]
    public class GymGeofenceBootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (context == null)
                return;

            GymGeofenceRegistrar.TryRegister(context.ApplicationContext ?? context);
        }
    }
}
