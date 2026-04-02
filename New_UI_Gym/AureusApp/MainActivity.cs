using AureusApp.Models;
using Android.Graphics;
using Android.Views;

namespace AureusApp;

[Activity(Label = "@string/app_name", MainLauncher = true)]
public class MainActivity : Activity
{
    private readonly List<TrainingExercise> _exercises = BuildInitialExercises();

    private LinearLayout? _dayMon;
    private LinearLayout? _dayTue;
    private LinearLayout? _dayToday;
    private LinearLayout? _dayThu;
    private LinearLayout? _dayFri;

    private LinearLayout? _navHome;
    private LinearLayout? _navWorkouts;
    private LinearLayout? _navTrain;
    private LinearLayout? _navProfile;

    private LinearLayout? _exerciseListContainer;
    private TextView? _workoutDurationLabel;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        BindViews();
        WireDateHandlers();
        WireBottomNavHandlers();
        WireActionButtons();

        SelectDate(_dayToday);
        SelectBottomNav(_navTrain);
        RenderExerciseSections();
        UpdateWorkoutDurationLabel();
    }

    private void BindViews()
    {
        _dayMon = FindViewById<LinearLayout>(Resource.Id.dayMon);
        _dayTue = FindViewById<LinearLayout>(Resource.Id.dayTue);
        _dayToday = FindViewById<LinearLayout>(Resource.Id.dayToday);
        _dayThu = FindViewById<LinearLayout>(Resource.Id.dayThu);
        _dayFri = FindViewById<LinearLayout>(Resource.Id.dayFri);

        _navHome = FindViewById<LinearLayout>(Resource.Id.navHome);
        _navWorkouts = FindViewById<LinearLayout>(Resource.Id.navWorkouts);
        _navTrain = FindViewById<LinearLayout>(Resource.Id.navTrain);
        _navProfile = FindViewById<LinearLayout>(Resource.Id.navProfile);

        _exerciseListContainer = FindViewById<LinearLayout>(Resource.Id.exerciseListContainer);
        _workoutDurationLabel = FindViewById<TextView>(Resource.Id.workoutDurationLabel);
    }

    private void WireDateHandlers()
    {
        if (_dayMon != null) _dayMon.Click += (_, _) => SelectDate(_dayMon);
        if (_dayTue != null) _dayTue.Click += (_, _) => SelectDate(_dayTue);
        if (_dayToday != null) _dayToday.Click += (_, _) => SelectDate(_dayToday);
        if (_dayThu != null) _dayThu.Click += (_, _) => SelectDate(_dayThu);
        if (_dayFri != null) _dayFri.Click += (_, _) => SelectDate(_dayFri);
    }

    private void SelectDate(LinearLayout? selected)
    {
        var days = new[] { _dayMon, _dayTue, _dayToday, _dayThu, _dayFri };
        foreach (var day in days)
        {
            if (day == null) continue;

            var isSelected = day == selected;
            day.Background = GetDrawable(isSelected ? Resource.Drawable.bg_day_selected : Resource.Drawable.bg_day_unselected);

            var label = day.FindViewById<TextView>(Resource.Id.dayLabel);
            var number = day.FindViewById<TextView>(Resource.Id.dayNumber);

            label?.SetTextColor(Android.Graphics.Color.ParseColor(isSelected ? "#3C2F00" : "#68D0C5AF"));
            number?.SetTextColor(Android.Graphics.Color.ParseColor(isSelected ? "#3C2F00" : "#68E5E2E1"));
        }
    }

    private void WireBottomNavHandlers()
    {
        if (_navHome != null) _navHome.Click += (_, _) => SelectBottomNav(_navHome);
        if (_navWorkouts != null) _navWorkouts.Click += (_, _) => SelectBottomNav(_navWorkouts);
        if (_navTrain != null) _navTrain.Click += (_, _) => SelectBottomNav(_navTrain);
        if (_navProfile != null) _navProfile.Click += (_, _) => SelectBottomNav(_navProfile);
    }

    private void SelectBottomNav(LinearLayout? selected)
    {
        ApplyNavState(_navHome, Resource.Id.navIconHome, Resource.Id.navLabelHome, selected == _navHome);
        ApplyNavState(_navWorkouts, Resource.Id.navIconWorkouts, Resource.Id.navLabelWorkouts, selected == _navWorkouts);
        ApplyNavState(_navTrain, Resource.Id.navIconTrain, Resource.Id.navLabelTrain, selected == _navTrain);
        ApplyNavState(_navProfile, Resource.Id.navIconProfile, Resource.Id.navLabelProfile, selected == _navProfile);
    }

    private void ApplyNavState(LinearLayout? container, int iconId, int labelId, bool isSelected)
    {
        if (container == null) return;

        var icon = container.FindViewById<TextView>(iconId);
        var label = container.FindViewById<TextView>(labelId);

        container.Background = GetDrawable(isSelected ? Resource.Drawable.bg_nav_selected : Resource.Drawable.bg_nav_item);
        icon?.SetTextColor(Android.Graphics.Color.ParseColor(isSelected ? "#3C2F00" : "#BEB9AF"));
        label?.SetTextColor(Android.Graphics.Color.ParseColor(isSelected ? "#3C2F00" : "#BEB9AF"));
    }

    private void WireActionButtons()
    {
        var finishWorkout = FindViewById<Button>(Resource.Id.finishWorkoutButton);
        if (finishWorkout != null)
        {
            finishWorkout.Click += (_, _) =>
            {
                var done = _exercises.SelectMany(x => x.Sets).Count(s => s.IsCompleted);
                var total = _exercises.SelectMany(x => x.Sets).Count();
                Toast.MakeText(this, $"Workout saved ({done}/{total} sets done)", ToastLength.Short)?.Show();
            };
        }
    }

    private void RenderExerciseSections()
    {
        if (_exerciseListContainer == null) return;

        _exerciseListContainer.RemoveAllViews();
        for (var i = 0; i < _exercises.Count; i++)
        {
            var section = BuildExerciseSection(_exercises[i], i);
            _exerciseListContainer.AddView(section);
        }
    }

    private View BuildExerciseSection(TrainingExercise exercise, int exerciseIndex)
    {
        var wrapper = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
        };
        wrapper.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = Dp(exerciseIndex == 0 ? 12 : 26),
        };

        wrapper.AddView(BuildExerciseHeader(exercise));

        if (exercise.StartCollapsed)
        {
            wrapper.AddView(BuildCollapsedSummary(exercise));
            return wrapper;
        }

        foreach (var set in exercise.Sets)
        {
            wrapper.AddView(BuildSetRow(exercise, set));
        }

        return wrapper;
    }

    private View BuildExerciseHeader(TrainingExercise exercise)
    {
        var header = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
        };
        header.SetGravity(GravityFlags.CenterVertical);

        var title = BuildText(exercise.Name, 28, true, "#E5E2E1");
        title.Typeface = Typeface.Create("sans-serif-condensed", TypefaceStyle.Bold);
        title.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);

        var subtitle = BuildText(exercise.Prescription, 10, true, "#F2CA50");

        header.AddView(title);
        header.AddView(subtitle);
        return header;
    }

    private View BuildCollapsedSummary(TrainingExercise exercise)
    {
        var panel = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
        };
        panel.SetGravity(GravityFlags.CenterVertical);
        panel.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = Dp(12),
        };
        panel.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
        panel.Background = GetDrawable(Resource.Drawable.bg_leg_press_panel);

        var completed = exercise.Sets.Count(s => s.IsCompleted);
        var setsBlock = BuildLabelValueBlock("SETS", $"{completed}/{exercise.Sets.Count}");
        setsBlock.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 3f);

        var weightBlock = BuildLabelValueBlock("TARGET WEIGHT", $"{exercise.TargetWeightKg ?? 0}kg");
        weightBlock.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 4f);

        var expandButton = new Button(this)
        {
            Text = "EXPAND SETS",
        };
        expandButton.SetTextColor(Android.Graphics.Color.ParseColor("#F2CA50"));
        expandButton.TextSize = 10f;
        expandButton.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
        expandButton.Background = GetDrawable(Resource.Drawable.bg_expand_button);
        expandButton.SetPadding(Dp(12), 0, Dp(12), 0);
        expandButton.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(38));
        expandButton.Click += (_, _) =>
        {
            exercise.StartCollapsed = false;
            RenderExerciseSections();
        };

        panel.AddView(setsBlock);
        panel.AddView(weightBlock);
        panel.AddView(expandButton);
        return panel;
    }

    private View BuildSetRow(TrainingExercise exercise, TrainingSet set)
    {
        var row = new LinearLayout(this)
        {
            Orientation = Orientation.Horizontal,
        };
        row.SetGravity(GravityFlags.CenterVertical);
        row.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = Dp(10),
        };
        row.SetPadding(Dp(10), Dp(10), Dp(10), Dp(10));

        var setCol = BuildLabelValueBlock("SET", set.Number.ToString());
        setCol.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 2f);
        ((LinearLayout)setCol).SetGravity(GravityFlags.Center);

        View weightCol;
        View repsCol;

        if (set.IsEditable)
        {
            weightCol = BuildEditableValue(set.WeightKg, value => set.WeightKg = value);
            weightCol.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 4f)
            {
                LeftMargin = Dp(6),
                RightMargin = Dp(6),
            };

            repsCol = BuildEditableValue(set.Reps, value => set.Reps = value, "15");
            repsCol.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 3f)
            {
                RightMargin = Dp(6),
            };
        }
        else
        {
            weightCol = BuildLabelValueBlock("WEIGHT", set.WeightKg.HasValue ? $"{set.WeightKg.Value} kg" : "--");
            weightCol.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 4f)
            {
                LeftMargin = Dp(8),
            };

            repsCol = BuildLabelValueBlock("REPS", set.Reps.HasValue ? set.Reps.Value.ToString() : "--");
            repsCol.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 3f);
        }

        var action = new TextView(this)
        {
            Text = set.IsCompleted ? "✓" : "○",
            Gravity = GravityFlags.Center,
            TextSize = 18f,
        };
        action.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
        action.LayoutParameters = new LinearLayout.LayoutParams(Dp(40), Dp(40));

        ApplySetRowState(row, action, set.IsCompleted);

        action.Click += (_, _) =>
        {
            set.IsCompleted = !set.IsCompleted;
            ApplySetRowState(row, action, set.IsCompleted);
            UpdateWorkoutDurationLabel();
            if (exercise.StartCollapsed)
            {
                RenderExerciseSections();
            }
        };

        row.AddView(setCol);
        row.AddView(weightCol);
        row.AddView(repsCol);
        row.AddView(action);
        return row;
    }

    private void ApplySetRowState(LinearLayout row, TextView action, bool completed)
    {
        row.Background = GetDrawable(completed ? Resource.Drawable.bg_set_row_complete : Resource.Drawable.bg_set_row_pending);
        action.Text = completed ? "✓" : "○";
        action.Background = GetDrawable(completed ? Resource.Drawable.bg_set_action_complete : Resource.Drawable.bg_set_action_idle);
        action.SetTextColor(Android.Graphics.Color.ParseColor(completed ? "#3C2F00" : "#66E5E2E1"));
    }

    private View BuildEditableValue(int? value, Action<int?> onChanged, string? hint = null)
    {
        var input = new EditText(this)
        {
            Text = value?.ToString() ?? string.Empty,
            Hint = hint ?? string.Empty,
            InputType = Android.Text.InputTypes.ClassNumber,
        };

        input.SetTextColor(Android.Graphics.Color.ParseColor("#E5E2E1"));
        input.SetHintTextColor(Android.Graphics.Color.ParseColor("#D0C5AF"));
        input.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
        input.TextSize = 18f;
        input.Background = GetDrawable(Resource.Drawable.bg_input_bottom_line);

        input.TextChanged += (_, _) =>
        {
            if (int.TryParse(input.Text, out var parsed))
            {
                onChanged(parsed);
            }
            else if (string.IsNullOrWhiteSpace(input.Text))
            {
                onChanged(null);
            }
        };

        return input;
    }

    private View BuildLabelValueBlock(string labelText, string valueText)
    {
        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
        };

        var label = BuildText(labelText, 10, true, "#D0C5AF");
        var value = BuildText(valueText, 22, true, "#E5E2E1");

        layout.AddView(label);
        layout.AddView(value);
        return layout;
    }

    private TextView BuildText(string text, int sizeSp, bool bold, string colorHex)
    {
        var textView = new TextView(this)
        {
            Text = text,
            TextSize = sizeSp,
        };
        textView.SetTextColor(Android.Graphics.Color.ParseColor(colorHex));
        textView.SetTypeface(Typeface.Default, bold ? TypefaceStyle.Bold : TypefaceStyle.Normal);
        return textView;
    }

    private void UpdateWorkoutDurationLabel()
    {
        if (_workoutDurationLabel == null) return;

        var completed = _exercises.SelectMany(x => x.Sets).Count(s => s.IsCompleted);
        var total = _exercises.SelectMany(x => x.Sets).Count();
        _workoutDurationLabel.Text = $"WORKOUT DURATION: 42M 15S  •  SETS DONE: {completed}/{total}";
    }

    private int Dp(int value)
    {
        var density = Resources?.DisplayMetrics?.Density ?? 1f;
        return (int)(value * density);
    }

    private static List<TrainingExercise> BuildInitialExercises()
    {
        return
        [
            new TrainingExercise
            {
                Name = "BARBELL BACK SQUAT",
                Prescription = "3 SETS • 8-10 REPS",
                Sets =
                [
                    new TrainingSet { Number = 1, WeightKg = 100, Reps = 8, IsCompleted = true, IsEditable = false },
                    new TrainingSet { Number = 2, WeightKg = 100, Reps = null, IsCompleted = false, IsEditable = false },
                    new TrainingSet { Number = 3, WeightKg = 100, Reps = null, IsCompleted = false, IsEditable = false },
                ],
            },
            new TrainingExercise
            {
                Name = "LEG PRESS",
                Prescription = "3 SETS • 12 REPS",
                StartCollapsed = true,
                TargetWeightKg = 180,
                Sets =
                [
                    new TrainingSet { Number = 1, WeightKg = 180, Reps = null, IsCompleted = false, IsEditable = false },
                    new TrainingSet { Number = 2, WeightKg = 180, Reps = null, IsCompleted = false, IsEditable = false },
                    new TrainingSet { Number = 3, WeightKg = 180, Reps = null, IsCompleted = false, IsEditable = false },
                ],
            },
            new TrainingExercise
            {
                Name = "CALF RAISES",
                Prescription = "4 SETS • 15 REPS",
                Sets =
                [
                    new TrainingSet { Number = 1, WeightKg = 60, Reps = null, IsCompleted = false, IsEditable = true },
                    new TrainingSet { Number = 2, WeightKg = 60, Reps = null, IsCompleted = false, IsEditable = true },
                    new TrainingSet { Number = 3, WeightKg = 60, Reps = null, IsCompleted = false, IsEditable = true },
                    new TrainingSet { Number = 4, WeightKg = 60, Reps = null, IsCompleted = false, IsEditable = true },
                ],
            },
        ];
    }
}
