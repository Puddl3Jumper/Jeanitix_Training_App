using Android.App;
using Android.Graphics;
using Android.Widget;

namespace Gym_App;

internal static class DialogThemeHelper
{
    internal static void StyleInput(Activity activity, EditText? input)
    {
        if (activity == null || input == null)
            return;

        input.SetTextColor(new Color(activity.GetColor(Resource.Color.color_text_primary)));
        input.SetHintTextColor(new Color(activity.GetColor(Resource.Color.color_text_secondary)));
        input.SetTextSize(Android.Util.ComplexUnitType.Sp, 14f);
        input.SetTypeface(Typeface.Create("sans-serif", TypefaceStyle.Normal), TypefaceStyle.Normal);
    }

    internal static void StyleShownDialog(Activity activity, AndroidX.AppCompat.App.AlertDialog? dialog, bool styleButtons = true)
    {
        if (activity == null || dialog == null)
            return;

        dialog.Window?.SetBackgroundDrawableResource(Resource.Drawable.bg_card_today_outer);

        var titleView = dialog.FindViewById<TextView>(Android.Resource.Id.Title);
        if (titleView == null)
        {
            var alertTitleId = activity.Resources?.GetIdentifier("alertTitle", "id", "android") ?? 0;
            if (alertTitleId != 0)
                titleView = dialog.FindViewById<TextView>(alertTitleId);
        }
        if (titleView != null)
        {
            titleView.SetTextColor(new Color(activity.GetColor(Resource.Color.color_text_primary)));
            titleView.SetTextSize(Android.Util.ComplexUnitType.Sp, 20f);
            titleView.SetTypeface(null, TypefaceStyle.Bold);
        }

        var messageView = dialog.FindViewById<TextView>(Android.Resource.Id.Message);
        if (messageView != null)
        {
            messageView.SetTextColor(new Color(activity.GetColor(Resource.Color.color_text_secondary)));
            messageView.SetTextSize(Android.Util.ComplexUnitType.Sp, 14f);
            messageView.SetTypeface(Typeface.Create("sans-serif", TypefaceStyle.Normal), TypefaceStyle.Normal);
        }

        if (!styleButtons)
            return;

        StyleDialogActionButton(activity, dialog.GetButton((int)Android.Content.DialogButtonType.Positive));
        StyleDialogActionButton(activity, dialog.GetButton((int)Android.Content.DialogButtonType.Negative));
        StyleDialogActionButton(activity, dialog.GetButton((int)Android.Content.DialogButtonType.Neutral));
    }

    private static void StyleDialogActionButton(Activity activity, Button? button)
    {
        if (activity == null || button == null)
            return;

        button.SetAllCaps(false);
        button.SetBackgroundResource(Resource.Drawable.bg_button_primary);
        button.SetTextColor(new Color(activity.GetColor(Android.Resource.Color.Black)));
        button.SetTextSize(Android.Util.ComplexUnitType.Sp, 16f);
        button.SetTypeface(null, TypefaceStyle.Bold);
    }
}