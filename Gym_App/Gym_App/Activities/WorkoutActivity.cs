using Android.Views;
using Android.Widget;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Text.Style;
using Gym_App;
using Gym_App.Data;
using Gym_App.Models;
using Google.Android.Material.Dialog;

namespace Gym_App.Activities
{
    [Activity(Label = "Workout Session")]
    public class WorkoutActivity : Activity
    {
        private GymDatabase? _database;
        private WorkoutSession? _currentWorkout;
        private TextView? _workoutTimeText;
        private TextView? _workoutDateText;
        private LinearLayout? _exercisesContainer;
        private System.Threading.Timer? _timer;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_workout);

            _database = new GymDatabase();
            
            _workoutTimeText = FindViewById<TextView>(Resource.Id.workoutTimeText);
            _workoutDateText = FindViewById<TextView>(Resource.Id.workoutDateText);
            _exercisesContainer = FindViewById<LinearLayout>(Resource.Id.exercisesContainer);
            
            var addExerciseButton = FindViewById<TextView>(Resource.Id.addExerciseButton);
            var finishWorkoutButton = FindViewById<TextView>(Resource.Id.finishWorkoutButton);
            var homeTab = FindViewById<LinearLayout>(Resource.Id.homeTab);
            var diaryTab = FindViewById<LinearLayout>(Resource.Id.diaryTab);
            var workoutTab = FindViewById<LinearLayout>(Resource.Id.workoutTab);
            var profileTab = FindViewById<LinearLayout>(Resource.Id.profileTab);

            if (homeTab != null) homeTab.Selected = false;
            if (diaryTab != null) diaryTab.Selected = false;
            if (workoutTab != null) workoutTab.Selected = true;
            if (profileTab != null) profileTab.Selected = false;
            UpdateBottomNavLabelStyles();

            if (homeTab != null)
            {
                homeTab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(HomeActivity)));
                };
            }

            if (diaryTab != null)
            {
                diaryTab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(HistoryActivity)));
                };
            }

            if (profileTab != null)
            {
                profileTab.Click += (s, e) =>
                {
                    StartActivity(new Intent(this, typeof(ProfileActivity)));
                };
            }

            int workoutId = Intent?.GetIntExtra("workoutId", -1) ?? -1;
            
            if (workoutId == -1)
            {
                _currentWorkout = _database.GetCurrentWorkout() ?? _database.CreateWorkoutSession("Workout Session");
            }
            else
            {
                _currentWorkout = _database.GetWorkoutSession(workoutId)
                                 ?? _database.GetCurrentWorkout()
                                 ?? _database.CreateWorkoutSession("Workout Session");
            }

            if (addExerciseButton != null)
            {
                addExerciseButton.Text = "Add Exercise / Circuit";
                addExerciseButton.Click += AddExerciseButton_Click;
            }
            
            if (finishWorkoutButton != null)
                finishWorkoutButton.Click += FinishWorkoutButton_Click;

            if (_workoutDateText != null)
            {
                _workoutDateText.Click += (s, e) => ShowWorkoutDatePicker();
            }

            UpdateUI();
            StartTimer();
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

        private void StartTimer()
        {
            _timer = new System.Threading.Timer(_ => 
            {
                RunOnUiThread(UpdateDuration);
            }, null, 0, 1000);
        }

        private void UpdateDuration()
        {
            if (_currentWorkout != null && _workoutTimeText != null)
            {
                var duration = _currentWorkout.Duration;
                _workoutTimeText.Text = $"Duration: {duration:hh\\:mm\\:ss}";
            }
        }

        private void UpdateUI()
        {
            if (_currentWorkout == null || _exercisesContainer == null)
                return;

            if (_workoutDateText != null)
            {
                _workoutDateText.Text = $"{_currentWorkout.StartTime:yyyy-MM-dd}";
            }
            _exercisesContainer.RemoveAllViews();

            foreach (var workoutExercise in _currentWorkout.Exercises)
            {
                AddExerciseView(workoutExercise);
            }
        }

        private void AddExerciseView(WorkoutExercise workoutExercise)
        {
            if (_exercisesContainer == null || workoutExercise.Exercise == null)
                return;

            var sectionContainer = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };

            var sectionLayoutParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent);
            sectionLayoutParams.SetMargins(0, 0, 0, 24);
            sectionContainer.LayoutParameters = sectionLayoutParams;
            sectionContainer.SetBackgroundResource(Resource.Drawable.bg_exercise_section);
            sectionContainer.SetPadding(DpToPx(16), DpToPx(16), DpToPx(16), DpToPx(16));

            var exerciseCard = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            
            var layoutParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent);
            layoutParams.SetMargins(0, 0, 0, 0);
            exerciseCard.LayoutParameters = layoutParams;
            exerciseCard.SetPadding(0, 0, 0, 0);

            string titleText = workoutExercise.Exercise.Name;
            if (!string.IsNullOrWhiteSpace(workoutExercise.CircuitName))
            {
                titleText = $"{workoutExercise.Exercise.Name} (Circuit: {workoutExercise.CircuitName} #{workoutExercise.CircuitOrder})";
            }

            var exerciseTitle = new TextView(this)
            {
                Text = titleText,
                TextSize = 20
            };
            exerciseTitle.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_primary)));
            exerciseTitle.Typeface = Typeface.Create("sans-serif-medium", TypefaceStyle.Normal);
            exerciseTitle.SetLineSpacing(0f, 1.4f);
            exerciseTitle.SetCompoundDrawablesWithIntrinsicBounds(GetExerciseIconResource(workoutExercise.Exercise.Name), 0, 0, 0);
            exerciseTitle.CompoundDrawablePadding = DpToPx(8);
            exerciseTitle.SetPadding(0, 0, 0, 8);
            sectionContainer.AddView(exerciseTitle);

            var setListContainer = new LinearLayout(this)
            {
                Orientation = Orientation.Vertical
            };
            setListContainer.SetBackgroundResource(Resource.Drawable.bg_set_item);
            setListContainer.SetPadding(DpToPx(16), DpToPx(8), DpToPx(16), DpToPx(8));
            var setListLayoutParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent);
            setListLayoutParams.SetMargins(0, 0, 0, DpToPx(8));
            setListContainer.LayoutParameters = setListLayoutParams;

            for (int index = 0; index < workoutExercise.Sets.Count; index++)
            {
                var set = workoutExercise.Sets[index];
                var setText = new TextView(this)
                {
                    TextSize = 16
                };
                setText.SetText(BuildStyledSetLine(workoutExercise, set), TextView.BufferType.Spannable);
                setText.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
                setText.SetLineSpacing(0f, 1.4f);
                setText.SetPadding(0, 0, 0, 0);

                    var setId = set.Id;
                    setText.Click += (_, __) => ShowEditSetDialog(setId);
                    setText.LongClick += (_, __) => ConfirmDeleteSet(setId);

                var setLayoutParams = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.WrapContent);
                setLayoutParams.SetMargins(0, 0, 0, index == workoutExercise.Sets.Count - 1 ? 0 : DpToPx(8));
                setText.LayoutParameters = setLayoutParams;
                setListContainer.AddView(setText);
            }

            if (workoutExercise.Sets.Count > 0)
            {
                exerciseCard.AddView(setListContainer);
            }

            var addSetButton = new Button(this)
            {
                Text = "ADD SET"
            };
            var buttonSpacing = DpToPx(12);
            var interButtonSpacing = DpToPx(8);
            var addSetLayoutParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent);
            addSetLayoutParams.SetMargins(0, buttonSpacing, 0, 0);
            addSetButton.LayoutParameters = addSetLayoutParams;
            addSetButton.SetBackgroundResource(Resource.Drawable.bg_button_train_action);
            addSetButton.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_on_primary)));
            addSetButton.TextSize = 16;
            addSetButton.SetTypeface(null, TypefaceStyle.Bold);
            addSetButton.SetLineSpacing(0f, 1.4f);
            addSetButton.SetMinimumHeight(DpToPx(43));
            addSetButton.SetPadding(DpToPx(12), DpToPx(10), DpToPx(12), DpToPx(10));
            addSetButton.Click += (s, e) => ShowAddSetDialog(workoutExercise);
            exerciseCard.AddView(addSetButton);

            if (workoutExercise.Sets.Count > 0)
            {
                var copySetButton = new Button(this)
                {
                    Text = "COPY PREVIOUS SET"
                };
                var copySetLayoutParams = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.WrapContent);
                copySetLayoutParams.SetMargins(0, interButtonSpacing, 0, 0);
                copySetButton.LayoutParameters = copySetLayoutParams;
                copySetButton.SetBackgroundResource(Resource.Drawable.bg_button_train_action);
                copySetButton.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_on_primary)));
                copySetButton.TextSize = 16;
                copySetButton.SetTypeface(null, TypefaceStyle.Bold);
                copySetButton.SetLineSpacing(0f, 1.4f);
                copySetButton.Click += (s, e) => CopyPreviousSet(workoutExercise);
                exerciseCard.AddView(copySetButton);
            }

            sectionContainer.AddView(exerciseCard);
            _exercisesContainer.AddView(sectionContainer);
        }

        private ISpannable BuildStyledSetLine(WorkoutExercise workoutExercise, WorkoutSet set)
        {
            var setLabel = $"Set {set.SetNumber}";
            var repsPart = $"{set.Reps} reps";
            var weightPart = $"{set.Weight} {set.WeightUnit}";

            string core = !string.IsNullOrWhiteSpace(workoutExercise.CircuitName)
                ? $"{setLabel} · Loop {set.LoopNumber} · {repsPart} · {weightPart}"
                : $"{setLabel} · {repsPart} · {weightPart}";

            if (!string.IsNullOrWhiteSpace(set.Notes))
            {
                core += $" · Note: {set.Notes}";
            }

            core += "  ✏️  🗑️";

            var spannable = new SpannableString(core);

            void BoldSubstring(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return;

                var start = core.IndexOf(value, StringComparison.Ordinal);
                if (start < 0)
                    return;

                spannable.SetSpan(new StyleSpan(TypefaceStyle.Bold), start, start + value.Length, SpanTypes.ExclusiveExclusive);
            }

            BoldSubstring(setLabel);
            BoldSubstring(repsPart);
            BoldSubstring(weightPart);

            return spannable;
        }

        private void ShowAddSetDialog(WorkoutExercise workoutExercise)
        {
            var dialogBuilder = new MaterialAlertDialogBuilder(this);

            var titleView = new TextView(this)
            {
                Text = "Add Set"
            };
            titleView.SetTextSize(Android.Util.ComplexUnitType.Sp, 20f);
            titleView.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.color_text_primary)));
            titleView.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Bold), TypefaceStyle.Bold);
            titleView.SetPadding(DpToPx(24), DpToPx(20), DpToPx(24), DpToPx(8));
            dialogBuilder.SetCustomTitle(titleView);

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(32, 16, 32, 16);

            var repsInput = new EditText(this) { Hint = "Reps", InputType = Android.Text.InputTypes.ClassNumber };
            var userPrefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            var unit = userPrefs?.GetString("unit", "lb") ?? "lb";
            var weightInput = new EditText(this) { Hint = $"Weight ({unit})", InputType = Android.Text.InputTypes.ClassNumber | Android.Text.InputTypes.NumberFlagDecimal };
            var loopInput = new EditText(this) { Hint = "Loop Number", InputType = Android.Text.InputTypes.ClassNumber };
            var notesInput = new EditText(this) { Hint = "Notes (optional)" };

            StyleDialogInput(repsInput);
            StyleDialogInput(weightInput);
            StyleDialogInput(loopInput);
            StyleDialogInput(notesInput);

            bool isCircuitExercise = !string.IsNullOrWhiteSpace(workoutExercise.CircuitName);
            int suggestedLoop = workoutExercise.Sets.Count == 0
                ? 1
                : workoutExercise.Sets.Max(s => s.LoopNumber);

            loopInput.Text = suggestedLoop.ToString();
            loopInput.Visibility = isCircuitExercise ? ViewStates.Visible : ViewStates.Gone;

            layout.AddView(repsInput);
            layout.AddView(weightInput);
            if (isCircuitExercise)
            {
                layout.AddView(loopInput);
            }
            layout.AddView(notesInput);
            var actionRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            actionRow.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            {
                TopMargin = DpToPx(14)
            };

            var cancelButton = new Button(this) { Text = "Cancel" };
            var addButton = new Button(this) { Text = "Add" };

            var cancelLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            {
                RightMargin = DpToPx(6)
            };
            var addLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            {
                LeftMargin = DpToPx(6)
            };

            cancelButton.LayoutParameters = cancelLp;
            addButton.LayoutParameters = addLp;

            StyleDialogActionButton(cancelButton);
            StyleDialogActionButton(addButton);

            actionRow.AddView(cancelButton);
            actionRow.AddView(addButton);
            layout.AddView(actionRow);

            dialogBuilder.SetView(layout);

            var addSetDialog = dialogBuilder.Create();
            addSetDialog.Show();
            DialogThemeHelper.StyleShownDialog(this, addSetDialog, styleButtons: false);

            cancelButton.Click += (s, e) => addSetDialog.Dismiss();
            addButton.Click += (s, e) =>
            {
                if (int.TryParse(repsInput.Text, out int reps) &&
                    double.TryParse(weightInput.Text, out double weight))
                {
                    int loopNumber = 1;
                    if (isCircuitExercise)
                    {
                        if (!int.TryParse(loopInput.Text, out loopNumber) || loopNumber < 1)
                        {
                            loopNumber = 1;
                        }
                    }

                    _database?.AddSetToExercise(workoutExercise.Id, reps, weight, notesInput.Text, loopNumber, unit);
                    _currentWorkout = _database?.GetWorkoutSession(_currentWorkout?.Id ?? -1);
                    UpdateUI();
                    addSetDialog.Dismiss();
                }
            };
        }

        private void StyleDialogInput(EditText input)
        {
            DialogThemeHelper.StyleInput(this, input);
        }

        private void StyleDialogActionButton(Button? button)
        {
            if (button == null)
                return;

            button.SetAllCaps(false);
            button.SetBackgroundResource(Resource.Drawable.bg_button_primary);
            button.SetTextColor(new Color(GetColor(Android.Resource.Color.Black)));
            button.SetTextSize(Android.Util.ComplexUnitType.Sp, 16f);
            button.SetTypeface(null, TypefaceStyle.Bold);
            button.SetPadding(DpToPx(18), DpToPx(8), DpToPx(18), DpToPx(8));
        }

        private void CopyPreviousSet(WorkoutExercise workoutExercise)
        {
            var latestSet = workoutExercise.Sets
                .OrderByDescending(s => s.SetNumber)
                .FirstOrDefault();

            if (latestSet == null)
                return;

            _database?.AddSetToExercise(
                workoutExercise.Id,
                latestSet.Reps,
                latestSet.Weight,
                latestSet.Notes,
                latestSet.LoopNumber,
                latestSet.WeightUnit);

            _currentWorkout = _database?.GetWorkoutSession(_currentWorkout?.Id ?? -1);
            UpdateUI();
            Toast.MakeText(this, "Previous set copied", ToastLength.Short)?.Show();
        }

        private void ShowWorkoutDatePicker()
        {
            if (_database == null || _currentWorkout == null)
                return;

            var currentDate = _currentWorkout.StartTime;
            var datePicker = new DatePicker(this);
            datePicker.UpdateDate(currentDate.Year, currentDate.Month - 1, currentDate.Day);

            var dateDialog = new MaterialAlertDialogBuilder(this)
                .SetTitle("Select Date")
                .SetView(datePicker)
                .SetPositiveButton("OK", (s, e) =>
                {
                    var selectedDate = new DateTime(datePicker.Year, datePicker.Month + 1, datePicker.DayOfMonth);
                    _database.UpdateWorkoutSession(_currentWorkout.Id, _currentWorkout.Name, selectedDate, _currentWorkout.Notes);
                    _currentWorkout = _database.GetWorkoutSession(_currentWorkout.Id);
                    UpdateUI();
                })
                .SetNegativeButton("Cancel", (s, e) => { })
                .Show();

            DialogThemeHelper.StyleShownDialog(this, dateDialog);
        }

        private void ShowEditSetDialog(int setId)
        {
            if (_database == null || _currentWorkout == null)
                return;

            var targetSet = _currentWorkout.Exercises
                .SelectMany(e => e.Sets)
                .FirstOrDefault(s => s.Id == setId);

            if (targetSet == null)
                return;

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(DpToPx(20), DpToPx(12), DpToPx(20), DpToPx(4));

            var repsInput = new EditText(this)
            {
                Hint = "Reps",
                InputType = InputTypes.ClassNumber,
                Text = targetSet.Reps.ToString()
            };

            var weightInput = new EditText(this)
            {
                Hint = $"Weight ({targetSet.WeightUnit})",
                InputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal,
                Text = targetSet.Weight.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            var notesInput = new EditText(this)
            {
                Hint = "Notes (optional)",
                Text = targetSet.Notes ?? string.Empty
            };

            StyleDialogInput(repsInput);
            StyleDialogInput(weightInput);
            StyleDialogInput(notesInput);

            layout.AddView(repsInput);
            layout.AddView(weightInput);
            layout.AddView(notesInput);

            var editDialog = new MaterialAlertDialogBuilder(this)
                .SetTitle($"Edit Set {targetSet.SetNumber}")
                .SetView(layout)
                .SetPositiveButton("Save", (s, e) =>
                {
                    if (!int.TryParse(repsInput.Text?.Trim(), out var reps) || reps <= 0)
                    {
                        Toast.MakeText(this, "Enter valid reps", ToastLength.Short)?.Show();
                        return;
                    }

                    var weightText = weightInput.Text?.Trim() ?? string.Empty;
                    if (!double.TryParse(weightText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var weight) || weight < 0)
                    {
                        Toast.MakeText(this, "Enter valid weight", ToastLength.Short)?.Show();
                        return;
                    }

                    _database.UpdateWorkoutSet(setId, reps, weight, notesInput.Text);
                    _currentWorkout = _database.GetWorkoutSession(_currentWorkout.Id);
                    UpdateUI();
                    Toast.MakeText(this, "Set updated", ToastLength.Short)?.Show();
                })
                .SetNegativeButton("Cancel", (s, e) => { })
                .Show();

            DialogThemeHelper.StyleShownDialog(this, editDialog);
        }

        private void ConfirmDeleteSet(int setId)
        {
            if (_database == null || _currentWorkout == null)
                return;

            DialogThemeHelper.ShowPillConfirmationDialog(
                this,
                title: "Delete set",
                message: "This will remove the set from the workout.",
                positiveText: "Delete",
                onPositive: () =>
                {
                    _database.DeleteWorkoutSet(setId);
                    _currentWorkout = _database.GetWorkoutSession(_currentWorkout.Id);
                    UpdateUI();
                    Toast.MakeText(this, "Set deleted", ToastLength.Short)?.Show();
                });
        }

        private void AddExerciseButton_Click(object? sender, EventArgs e)
        {
            var exercises = _database?.GetAllExercises() ?? new List<Exercise>();
            var exerciseNames = exercises.Select(ex => ex.Name).ToArray();

            ShowStyledSelectionDialog("Select Exercise", exerciseNames, selectedIndex =>
            {
                var selectedExercise = exercises[selectedIndex];
                ShowExerciseModeDialog(selectedExercise);
            });
        }

        private void ShowExerciseModeDialog(Exercise selectedExercise)
        {
            var options = new[]
            {
                "Add as Regular Exercise",
                "Add to Circuit"
            };

            ShowStyledSelectionDialog($"Add {selectedExercise.Name}", options, selectedIndex =>
            {
                if (selectedIndex == 0)
                {
                    _database?.AddExerciseToWorkout(_currentWorkout?.Id ?? -1, selectedExercise.Id);
                    _currentWorkout = _database?.GetWorkoutSession(_currentWorkout?.Id ?? -1);
                    UpdateUI();
                    return;
                }

                ShowCircuitNameDialog(selectedExercise);
            });
        }

        private void ShowStyledSelectionDialog(string title, string[] options, Action<int> onSelected)
        {
            var listView = new ListView(this);
            listView.SetBackgroundColor(new Color(GetColor(Resource.Color.color_surface)));
            listView.DividerHeight = 0;
            listView.SetPadding(DpToPx(16), DpToPx(12), DpToPx(16), DpToPx(12));
            listView.SetClipToPadding(false);

            var adapter = new YellowListAdapter(this, options);
            listView.Adapter = adapter;

            var titleView = new TextView(this)
            {
                Text = title,
                TextSize = 22f
            };
            titleView.SetPadding(DpToPx(24), DpToPx(20), DpToPx(24), DpToPx(8));
            titleView.SetTextColor(new Color(GetColor(Resource.Color.color_text_primary)));
            titleView.SetTypeface(null, TypefaceStyle.Bold);

            var dialog = new MaterialAlertDialogBuilder(this).Create();
            dialog.SetCustomTitle(titleView);
            dialog.SetView(listView);

            listView.ItemClick += (sender, args) =>
            {
                dialog.Dismiss();
                onSelected(args.Position);
            };

            dialog.Show();
            DialogThemeHelper.StyleShownDialog(this, dialog, styleButtons: false);
        }

        private sealed class YellowListAdapter : ArrayAdapter<string>
        {
            private readonly Activity _activity;

            public YellowListAdapter(Activity activity, string[] options)
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
                {
                    optionText.Text = GetItem(position) ?? string.Empty;
                }

                return view;
            }
        }

        private void ShowCircuitNameDialog(Exercise selectedExercise)
        {
            var dialog = new MaterialAlertDialogBuilder(this);

            var titleView = new TextView(this)
            {
                Text = "Circuit Name"
            };
            titleView.SetTextSize(Android.Util.ComplexUnitType.Sp, 20f);
            titleView.SetTextColor(new Color(GetColor(Resource.Color.color_text_primary)));
            titleView.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Bold), TypefaceStyle.Bold);
            titleView.SetPadding(DpToPx(24), DpToPx(20), DpToPx(24), DpToPx(8));
            dialog.SetCustomTitle(titleView);

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(32, 16, 32, 16);

            var circuitNameInput = new EditText(this)
            {
                Hint = "e.g., Circuit A"
            };

            StyleDialogInput(circuitNameInput);

            layout.AddView(circuitNameInput);

            var actionRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            actionRow.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            {
                TopMargin = DpToPx(14)
            };

            var cancelButton = new Button(this) { Text = "Cancel" };
            var addButton = new Button(this) { Text = "Add" };

            var cancelLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            {
                RightMargin = DpToPx(6)
            };
            var addLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            {
                LeftMargin = DpToPx(6)
            };

            cancelButton.LayoutParameters = cancelLp;
            addButton.LayoutParameters = addLp;

            StyleDialogActionButton(cancelButton);
            StyleDialogActionButton(addButton);

            actionRow.AddView(cancelButton);
            actionRow.AddView(addButton);
            layout.AddView(actionRow);

            dialog.SetView(layout);

            var shownDialog = dialog.Create();
            shownDialog.Show();
            DialogThemeHelper.StyleShownDialog(this, shownDialog, styleButtons: false);

            cancelButton.Click += (sender, args) => shownDialog.Dismiss();
            addButton.Click += (sender, args) =>
            {
                var circuitName = circuitNameInput.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(circuitName))
                {
                    _database?.AddExerciseToWorkout(_currentWorkout?.Id ?? -1, selectedExercise.Id, circuitName);
                    _currentWorkout = _database?.GetWorkoutSession(_currentWorkout?.Id ?? -1);
                    UpdateUI();
                    shownDialog.Dismiss();
                }
                else
                {
                    Toast.MakeText(this, "Circuit name is required", ToastLength.Short)?.Show();
                }
            };
        }

        private void FinishWorkoutButton_Click(object? sender, EventArgs e)
        {
            if (_database == null)
                return;

            if (_currentWorkout == null)
            {
                Toast.MakeText(this, "No active workout found", ToastLength.Short)?.Show();
                return;
            }

            if (_currentWorkout.Exercises.Count == 0)
            {
                Toast.MakeText(this, "Add at least one exercise before finishing", ToastLength.Short)?.Show();
                return;
            }

            var workoutId = _currentWorkout.Id;

            var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
            layout.SetPadding(DpToPx(24), DpToPx(18), DpToPx(24), DpToPx(18));

            var titleText = new TextView(this)
            {
                Text = "Finish Workout",
                TextSize = 22f
            };
            titleText.SetTextColor(new Color(GetColor(Resource.Color.color_text_primary)));
            titleText.SetTypeface(null, TypefaceStyle.Bold);

            var messageText = new TextView(this)
            {
                Text = "Are you sure you want to finish this workout?",
                TextSize = 16f
            };
            messageText.SetTextColor(new Color(GetColor(Resource.Color.color_text_secondary)));
            messageText.SetPadding(0, DpToPx(10), 0, DpToPx(16));

            var actionRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            var noButton = new Button(this) { Text = "No" };
            var yesButton = new Button(this) { Text = "Yes" };

            var noLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            {
                RightMargin = DpToPx(6)
            };
            var yesLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f)
            {
                LeftMargin = DpToPx(6)
            };

            noButton.LayoutParameters = noLp;
            yesButton.LayoutParameters = yesLp;

            StyleDialogActionButton(noButton);
            StyleDialogActionButton(yesButton);

            actionRow.AddView(noButton);
            actionRow.AddView(yesButton);

            layout.AddView(titleText);
            layout.AddView(messageText);
            layout.AddView(actionRow);

            var dialog = new MaterialAlertDialogBuilder(this)
                .SetView(layout)
                .Create();

            dialog.Show();
            dialog.Window?.SetBackgroundDrawableResource(Resource.Drawable.bg_card_today_outer);

            noButton.Click += (s, e) => dialog.Dismiss();
            yesButton.Click += (s, e) =>
            {
                dialog.Dismiss();
                _database.CompleteWorkout(workoutId);
                Toast.MakeText(this, "Workout completed!", ToastLength.Short)?.Show();

                var historyIntent = new Intent(this, typeof(HistoryActivity));
                historyIntent.AddFlags(ActivityFlags.ClearTop);
                StartActivity(historyIntent);
                Finish();
            };
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _timer?.Dispose();
        }

        private int DpToPx(int dp)
        {
            return (int)(dp * Resources.DisplayMetrics.Density);
        }

        private int GetExerciseIconResource(string exerciseName)
        {
            var name = (exerciseName ?? string.Empty).ToLowerInvariant();

            if (name.Contains("push") || name.Contains("bench") || name.Contains("press") || name.Contains("chest"))
                return Resource.Drawable.ic_fitness_center;

            if (name.Contains("squat") || name.Contains("lunge") || name.Contains("leg") || name.Contains("calf") || name.Contains("hamstring") || name.Contains("quad"))
                return Resource.Drawable.ic_accessibility_new;

            return Resource.Drawable.ic_dumbbell;
        }
    }
}
