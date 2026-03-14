using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Gym_App.Data;

namespace Gym_App.Activities
{
    [Activity(Label = "Personal Information")]
    public class EditProfileActivity : Activity
    {
        private const string ProfilePrefsName = "user_profile";
        private static readonly HttpClient Http = new();

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_edit_profile);

            var toolbar = FindViewById<Google.Android.Material.AppBar.MaterialToolbar>(Resource.Id.editProfileToolbar);
            if (toolbar != null)
            {
                toolbar.Title = "Personal Information";
            }

            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            if (homeTab != null) homeTab.Selected = false;
            if (diaryTab != null) diaryTab.Selected = false;
            if (workoutTab != null) workoutTab.Selected = false;
            if (profileTab != null) profileTab.Selected = true;
            UpdateBottomNavLabelStyles();

            if (homeTab != null)
            {
                homeTab.Click += (s, e) => StartActivity(new Intent(this, typeof(HomeActivity)));
            }

            if (diaryTab != null)
            {
                diaryTab.Click += (s, e) => StartActivity(new Intent(this, typeof(HistoryActivity)));
            }

            if (workoutTab != null)
            {
                workoutTab.Click += (s, e) => StartActivity(new Intent(this, typeof(WorkoutActivity)));
            }

            var nicknameInput = FindViewById<EditText>(Resource.Id.editNicknameInput);
            var weightInput = FindViewById<EditText>(Resource.Id.editWeightInput);
            var heightInput = FindViewById<EditText>(Resource.Id.editHeightInput);
            var ageInput = FindViewById<EditText>(Resource.Id.editAgeInput);
            var sexSelector = FindViewById<TextView>(Resource.Id.editSexSelector);
            var saveButton = FindViewById(Resource.Id.saveProfileButton);
            var cancelButton = FindViewById(Resource.Id.cancelEditProfileButton);

            if (weightInput != null)
            {
                weightInput.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;
            }

            if (ageInput != null)
            {
                ageInput.InputType = InputTypes.ClassNumber;
            }

            if (heightInput != null)
            {
                heightInput.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;
            }

            var sexOptions = new[] { "Not set", "Male", "Female", "Other" };

            var prefs = GetSharedPreferences(ProfilePrefsName, FileCreationMode.Private);
            var nickname = prefs?.GetString("full_name", string.Empty) ?? string.Empty;
            var weight = prefs?.GetString("current_weight", string.Empty) ?? string.Empty;
            var height = prefs?.GetString("height_cm", string.Empty) ?? string.Empty;
            var age = prefs?.GetString("age", string.Empty) ?? string.Empty;
            var sex = prefs?.GetString("sex", "Not set") ?? "Not set";
            var selectedSex = sexOptions.Contains(sex) ? sex : "Not set";

            if (TryParseNumber(height, out var heightNumeric) && heightNumeric > 20)
            {
                height = (heightNumeric / 30.48).ToString("0.##");
            }

            if (nicknameInput != null) nicknameInput.Text = nickname;
            if (weightInput != null && weight != "--") weightInput.Text = weight;
            if (heightInput != null && height != "--") heightInput.Text = height;
            if (ageInput != null && age != "--") ageInput.Text = age;

            if (sexSelector != null)
            {
                sexSelector.Text = selectedSex;
                sexSelector.Click += (s, e) =>
                {
                    ShowStyledSelectionDialog("Select Sex", sexOptions, selectedIndex =>
                    {
                        selectedSex = sexOptions[selectedIndex];
                        sexSelector.Text = selectedSex;
                    });
                };
            }

            if (saveButton != null)
            {
                saveButton.Click += async (s, e) =>
                {
                    var enteredNickname = nicknameInput?.Text?.Trim() ?? string.Empty;
                    var enteredWeight = weightInput?.Text?.Trim() ?? string.Empty;
                    var enteredHeight = heightInput?.Text?.Trim() ?? string.Empty;
                    var enteredAge = ageInput?.Text?.Trim() ?? string.Empty;
                    var selectedSexValue = selectedSex;

                    if (prefs == null)
                    {
                        Toast.MakeText(this, "Unable to save profile", ToastLength.Short)?.Show();
                        return;
                    }

                    var existingNickname = prefs.GetString("full_name", string.Empty) ?? string.Empty;
                    var existingWeight = prefs.GetString("current_weight", "--") ?? "--";
                    var existingHeight = prefs.GetString("height_cm", "--") ?? "--";
                    var existingAge = prefs.GetString("age", "--") ?? "--";

                    var nicknameToSave = string.IsNullOrWhiteSpace(enteredNickname) ? existingNickname : enteredNickname;
                    var weightToSave = string.IsNullOrWhiteSpace(enteredWeight) ? existingWeight : enteredWeight;
                    var heightToSave = string.IsNullOrWhiteSpace(enteredHeight) ? existingHeight : enteredHeight;
                    var ageToSave = string.IsNullOrWhiteSpace(enteredAge) ? existingAge : enteredAge;

                    var editor = prefs.Edit();
                    editor.PutString("full_name", nicknameToSave);
                    editor.PutString("current_weight", weightToSave);
                    editor.PutString("height_cm", heightToSave);
                    editor.PutString("age", ageToSave);
                    editor.PutString("sex", selectedSexValue);
                    editor.PutString("unit", "lb");

                    var currentEmail = (prefs.GetString("email", string.Empty) ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(currentEmail) && !string.IsNullOrWhiteSpace(nicknameToSave))
                    {
                        var nicknameKey = $"profile_full_name::{currentEmail.ToLowerInvariant()}";
                        editor.PutString(nicknameKey, nicknameToSave);
                    }

                    var saved = editor.Commit();
                    if (!saved)
                    {
                        Toast.MakeText(this, "Profile save failed. Please try again.", ToastLength.Short)?.Show();
                        return;
                    }

                    var cloudSynced = true;
                    if (!string.IsNullOrWhiteSpace(nicknameToSave))
                    {
                        try
                        {
                            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                            cloudSynced = await TryUpdateOnlineNicknameAsync(nicknameToSave, timeoutCts.Token);
                        }
                        catch
                        {
                            cloudSynced = false;
                        }
                    }

                    try
                    {
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                        var profileSynced = await TryUpdateOnlineProfileAsync(
                            nicknameToSave,
                            weightToSave,
                            heightToSave,
                            ageToSave,
                            selectedSexValue,
                            "lb",
                            timeoutCts.Token);
                        cloudSynced = cloudSynced && profileSynced;
                    }
                    catch
                    {
                        cloudSynced = false;
                    }

                    SetResult(Result.Ok);
                    Toast.MakeText(this, cloudSynced ? "Profile updated" : "Profile updated locally. Online nickname sync pending.", ToastLength.Short)?.Show();
                    Finish();
                };
            }

            if (cancelButton != null)
            {
                cancelButton.Click += (s, e) => Finish();
            }
        }

        private void UpdateBottomNavLabelStyles()
        {
            SetTabLabelStyle(Resource.Id.homeTabLabel, FindViewById<LinearLayout>(Resource.Id.homeTab)?.Selected == true);
            SetTabLabelStyle(Resource.Id.diaryTabLabel, FindViewById<LinearLayout>(Resource.Id.diaryTab)?.Selected == true);
            SetTabLabelStyle(Resource.Id.workoutTabLabel, FindViewById<LinearLayout>(Resource.Id.workoutTab)?.Selected == true);
            SetTabLabelStyle(Resource.Id.profileTabLabel, FindViewById<LinearLayout>(Resource.Id.profileTab)?.Selected == true);
        }

        private void SetTabLabelStyle(int labelId, bool isSelected)
        {
            var label = FindViewById<TextView>(labelId);
            if (label == null)
                return;

            label.SetTypeface(null, isSelected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        }

        private void ShowStyledSelectionDialog(string title, string[] options, Action<int> onSelected)
        {
            var adapter = new SelectionListAdapter(this, options);

            var builder = new Google.Android.Material.Dialog.MaterialAlertDialogBuilder(this);

            var titleView = new TextView(this)
            {
                Text = title,
                TextSize = 20f
            };
            titleView.SetTypeface(null, TypefaceStyle.Bold);
            titleView.SetTextColor(new Color(GetColor(Resource.Color.color_text_primary)));
            titleView.SetPadding(DpToPx(24), DpToPx(20), DpToPx(24), DpToPx(8));
            builder.SetCustomTitle(titleView);

            builder.SetAdapter(adapter, (s, e) => onSelected(e.Which));
            var dialog = builder.Show();
            DialogThemeHelper.StyleShownDialog(this, dialog, styleButtons: false);
        }

        private sealed class SelectionListAdapter : ArrayAdapter<string>
        {
            private readonly Activity _activity;

            public SelectionListAdapter(Activity activity, string[] options)
                : base(activity, Android.Resource.Layout.SimpleListItem1, options)
            {
                _activity = activity;
            }

            public override View GetView(int position, View? convertView, ViewGroup parent)
            {
                var view = convertView ?? _activity.LayoutInflater?.Inflate(Resource.Layout.item_selection_option, parent, false);
                if (view == null)
                    return base.GetView(position, convertView, parent);

                var optionText = view.FindViewById<TextView>(Resource.Id.selectionOptionText);
                if (optionText != null)
                    optionText.Text = GetItem(position) ?? string.Empty;

                return view;
            }
        }

        private int DpToPx(int dp)
        {
            return (int)(dp * Resources.DisplayMetrics.Density);
        }

        private async Task<bool> TryUpdateOnlineNicknameAsync(string nickname, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return true;

            if (!AuthSessionStore.HasSession(this))
                return true;

            var config = FirebaseProjectConfig.LoadFromGoogleServicesJson(this);
            var auth = new FirebaseAuthService(config);

            var (refreshToken, idToken, expiresAtUtc) = AuthSessionStore.ReadSessionTokens(this);
            var token = idToken ?? string.Empty;

            if (string.IsNullOrWhiteSpace(token) || !expiresAtUtc.HasValue || expiresAtUtc.Value <= DateTimeOffset.UtcNow.AddMinutes(1))
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                    return false;

                var refreshed = await auth.RefreshIdTokenAsync(refreshToken, cancellationToken);
                var email = AuthSessionStore.ReadEmail(this) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(refreshed.Email) && !string.IsNullOrWhiteSpace(email))
                    refreshed = refreshed with { Email = email };

                AuthSessionStore.Save(this, refreshed);
                token = refreshed.IdToken;
            }

            if (string.IsNullOrWhiteSpace(token))
                return false;

            var updated = await auth.UpdateProfileDisplayNameAsync(token, nickname, cancellationToken);
            var existingEmail = AuthSessionStore.ReadEmail(this) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(updated.Email) && !string.IsNullOrWhiteSpace(existingEmail))
                updated = updated with { Email = existingEmail };

            AuthSessionStore.Save(this, updated);
            return true;
        }

        private bool TryParseNumber(string value, out double number)
        {
            return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number) ||
                   double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out number);
        }

        private async Task<bool> TryUpdateOnlineProfileAsync(
            string nickname,
            string weight,
            string height,
            string age,
            string sex,
            string unit,
            CancellationToken cancellationToken)
        {
            if (!AuthSessionStore.HasSession(this))
                return true;

            var localId = AuthSessionStore.ReadLocalId(this) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(localId))
                return false;

            var config = FirebaseProjectConfig.LoadFromGoogleServicesJson(this);
            var idToken = await GetValidIdTokenAsync(config, cancellationToken);
            if (string.IsNullOrWhiteSpace(idToken))
                return false;

            var baseUrl = $"https://{config.ProjectId}-default-rtdb.firebaseio.com";
            var url = $"{baseUrl}/users/{Uri.EscapeDataString(localId)}/profile.json?auth={Uri.EscapeDataString(idToken)}";

            var payload = new
            {
                fullName = nickname,
                currentWeight = weight,
                heightCm = height,
                age,
                sex,
                unit,
                updatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Http.PutAsync(url, content, cancellationToken);
            return response.IsSuccessStatusCode;
        }

        private async Task<string?> GetValidIdTokenAsync(FirebaseProjectConfig config, CancellationToken cancellationToken)
        {
            var (refreshToken, idToken, expiresAtUtc) = AuthSessionStore.ReadSessionTokens(this);

            if (!string.IsNullOrWhiteSpace(idToken) && expiresAtUtc.HasValue && expiresAtUtc.Value > DateTimeOffset.UtcNow.AddMinutes(1))
                return idToken;

            if (string.IsNullOrWhiteSpace(refreshToken))
                return null;

            var auth = new FirebaseAuthService(config);
            var refreshed = await auth.RefreshIdTokenAsync(refreshToken, cancellationToken);
            var email = AuthSessionStore.ReadEmail(this) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(refreshed.Email) && !string.IsNullOrWhiteSpace(email))
                refreshed = refreshed with { Email = email };

            AuthSessionStore.Save(this, refreshed);
            return refreshed.IdToken;
        }
    }
}
