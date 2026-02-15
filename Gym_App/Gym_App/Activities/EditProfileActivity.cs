using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Widget;
using System;

namespace Gym_App.Activities
{
    [Activity(Label = "Edit Profile")]
    public class EditProfileActivity : Activity
    {
        private const string ProfilePrefsName = "user_profile";

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_edit_profile);

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
            var sexSpinner = FindViewById<Spinner>(Resource.Id.editSexSpinner);
            var saveButton = FindViewById<Button>(Resource.Id.saveProfileButton);
            var cancelButton = FindViewById<Button>(Resource.Id.cancelEditProfileButton);

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
            if (sexSpinner != null)
            {
                var adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, sexOptions);
                adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
                sexSpinner.Adapter = adapter;
            }

            var prefs = GetSharedPreferences(ProfilePrefsName, FileCreationMode.Private);
            var nickname = prefs?.GetString("full_name", string.Empty) ?? string.Empty;
            var weight = prefs?.GetString("current_weight", string.Empty) ?? string.Empty;
            var height = prefs?.GetString("height_cm", string.Empty) ?? string.Empty;
            var age = prefs?.GetString("age", string.Empty) ?? string.Empty;
            var sex = prefs?.GetString("sex", "Not set") ?? "Not set";

            if (nicknameInput != null) nicknameInput.Text = nickname;
            if (weightInput != null && weight != "--") weightInput.Text = weight;
            if (heightInput != null && height != "--") heightInput.Text = height;
            if (ageInput != null && age != "--") ageInput.Text = age;

            if (sexSpinner != null)
            {
                var index = Array.IndexOf(sexOptions, sex);
                sexSpinner.SetSelection(index >= 0 ? index : 0);
            }

            if (saveButton != null)
            {
                saveButton.Click += (s, e) =>
                {
                    var enteredNickname = nicknameInput?.Text?.Trim() ?? string.Empty;
                    var enteredWeight = weightInput?.Text?.Trim() ?? string.Empty;
                    var enteredHeight = heightInput?.Text?.Trim() ?? string.Empty;
                    var enteredAge = ageInput?.Text?.Trim() ?? string.Empty;
                    var selectedSex = sexSpinner?.SelectedItem?.ToString() ?? "Not set";

                    prefs?.Edit()?
                        .PutString("full_name", enteredNickname)
                        .PutString("current_weight", string.IsNullOrWhiteSpace(enteredWeight) ? "--" : enteredWeight)
                        .PutString("height_cm", string.IsNullOrWhiteSpace(enteredHeight) ? "--" : enteredHeight)
                        .PutString("age", string.IsNullOrWhiteSpace(enteredAge) ? "--" : enteredAge)
                        .PutString("sex", selectedSex)
                        ?.Apply();

                    Toast.MakeText(this, "Profile updated", ToastLength.Short)?.Show();
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
    }
}
