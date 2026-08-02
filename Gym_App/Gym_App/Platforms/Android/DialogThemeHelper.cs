using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace Gym_App;

internal static class DialogThemeHelper
{
    internal static AndroidX.AppCompat.App.AlertDialog? ShowPillConfirmationDialog(
        Activity activity,
        string title,
        string message,
        string positiveText,
        Action onPositive,
        string negativeText = "Cancel")
    {
        if (activity == null)
            return null;

        var density = activity.Resources?.DisplayMetrics?.Density ?? 1f;
        int DpToPx(int dp) => (int)(dp * density);

        var layout = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        layout.SetPadding(DpToPx(24), DpToPx(18), DpToPx(24), DpToPx(18));

        var titleText = new TextView(activity)
        {
            Text = title,
            TextSize = 22f
        };
        titleText.SetTextColor(new Color(activity.GetColor(Resource.Color.color_text_primary)));
        titleText.SetTypeface(null, TypefaceStyle.Bold);

        var messageText = new TextView(activity)
        {
            Text = message,
            TextSize = 16f
        };
        messageText.SetTextColor(new Color(activity.GetColor(Resource.Color.color_text_secondary)));
        messageText.SetPadding(0, DpToPx(10), 0, DpToPx(16));

        var actionRow = new LinearLayout(activity) { Orientation = Orientation.Horizontal };
        var cancelButton = new Button(activity) { Text = negativeText };
        var actionButton = new Button(activity) { Text = positiveText };

        var cancelLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
        {
            RightMargin = DpToPx(6)
        };
        var actionLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
        {
            LeftMargin = DpToPx(6)
        };

        cancelButton.LayoutParameters = cancelLp;
        actionButton.LayoutParameters = actionLp;

        StyleDialogActionButton(activity, cancelButton);
        StyleDialogActionButton(activity, actionButton);

        actionRow.AddView(cancelButton);
        actionRow.AddView(actionButton);

        layout.AddView(titleText);
        layout.AddView(messageText);
        layout.AddView(actionRow);

        var dialog = new Google.Android.Material.Dialog.MaterialAlertDialogBuilder(activity)
            .SetView(layout)
            .Create();

        dialog.Show();
        StyleShownDialog(activity, dialog, styleButtons: false);

        cancelButton.Click += (s, e) => dialog.Dismiss();
        actionButton.Click += (s, e) =>
        {
            onPositive();
            dialog.Dismiss();
        };

        return dialog;
    }

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
        if (titleView == null)
        {
            var appCompatAlertTitleId = activity.Resources?.GetIdentifier("alertTitle", "id", "androidx.appcompat") ?? 0;
            if (appCompatAlertTitleId != 0)
                titleView = dialog.FindViewById<TextView>(appCompatAlertTitleId);
        }
        if (titleView == null)
        {
            var materialAlertTitleId = activity.Resources?.GetIdentifier("alertTitle", "id", "com.google.android.material") ?? 0;
            if (materialAlertTitleId != 0)
                titleView = dialog.FindViewById<TextView>(materialAlertTitleId);
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
        button.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(new Color(activity.GetColor(Resource.Color.color_primary)));
        button.SetTextColor(new Color(activity.GetColor(Android.Resource.Color.Black)));
        button.SetTextSize(Android.Util.ComplexUnitType.Sp, 16f);
        button.SetTypeface(null, TypefaceStyle.Bold);
    }
}