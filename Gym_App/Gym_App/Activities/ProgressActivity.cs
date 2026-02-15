using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Gym_App.Data;

namespace Gym_App.Activities
{
    [Activity(Label = "Progress")]
    public class ProgressActivity : Activity
    {
        private GymDatabase? _database;
        private Spinner? _exerciseSpinner;
        private TextView? _prValueText;
        private TextView? _lastValueText;
        private TextView? _progressHintText;
        private FrameLayout? _chartContainer;
        private WeightLineChartView? _chartView;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            ThemeManager.ApplyTheme(this);
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_progress);

            _database = new GymDatabase();

            _exerciseSpinner = FindViewById<Spinner>(Resource.Id.exerciseSpinner);
            _prValueText = FindViewById<TextView>(Resource.Id.prValueText);
            _lastValueText = FindViewById<TextView>(Resource.Id.lastValueText);
            _progressHintText = FindViewById<TextView>(Resource.Id.progressHintText);
            _chartContainer = FindViewById<FrameLayout>(Resource.Id.chartContainer);

            if (_chartContainer != null)
            {
                _chartView = new WeightLineChartView(this);
                _chartContainer.AddView(_chartView, new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.MatchParent));
            }

            SetupBottomNav();
            SetupExercisePicker();
        }

        private void SetupBottomNav()
        {
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
                homeTab.Click += (s, e) => StartActivity(new Intent(this, typeof(HomeActivity)));
            if (diaryTab != null)
                diaryTab.Click += (s, e) => StartActivity(new Intent(this, typeof(HistoryActivity)));
            if (workoutTab != null)
                workoutTab.Click += (s, e) => StartActivity(new Intent(this, typeof(WorkoutActivity)));
            if (profileTab != null)
                profileTab.Click += (s, e) => StartActivity(new Intent(this, typeof(ProfileActivity)));
        }

        private void SetupExercisePicker()
        {
            if (_database == null || _exerciseSpinner == null)
                return;

            var exerciseNames = _database.GetAllExercises()
                .Select(exercise => exercise.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            if (exerciseNames.Count == 0)
            {
                if (_progressHintText != null)
                    _progressHintText.Text = "No exercises found yet";
                return;
            }

            var adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem, exerciseNames);
            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            _exerciseSpinner.Adapter = adapter;
            _exerciseSpinner.ItemSelected += (s, e) =>
            {
                var exerciseName = exerciseNames[e.Position];
                LoadProgress(exerciseName);
            };

            LoadProgress(exerciseNames[0]);
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

        private void LoadProgress(string exerciseName)
        {
            if (_database == null || _chartView == null)
                return;

            var unitPrefs = GetSharedPreferences("user_profile", FileCreationMode.Private);
            var unit = unitPrefs?.GetString("unit", "kg") ?? "kg";

            var points = _database.GetExerciseProgress(exerciseName, 30);
            var metrics = _database.GetExercisePrAndLastWeight(exerciseName);

            _chartView.SetData(points, unit);

            if (_prValueText != null)
                _prValueText.Text = $"PR: {Math.Round(metrics.pr, 1)} {unit}";

            if (_lastValueText != null)
                _lastValueText.Text = $"Last: {Math.Round(metrics.lastWeight, 1)} {unit}";

            if (_progressHintText != null)
            {
                _progressHintText.Text = points.Count < 2
                    ? "Add more sessions for a clearer trend line"
                    : $"Showing {points.Count} recent workout points";
            }
        }

        private sealed class WeightLineChartView : View
        {
            private readonly Paint _linePaint;
            private readonly Paint _pointPaint;
            private readonly Paint _axisPaint;
            private readonly Paint _labelPaint;
            private readonly Paint _emptyPaint;

            private List<(DateTime date, double maxWeight)> _points = new();
            private string _unit = "kg";

            public WeightLineChartView(Context context) : base(context)
            {
                _linePaint = new Paint(PaintFlags.AntiAlias)
                {
                    Color = Color.Rgb(248, 204, 27),
                    StrokeWidth = 5f
                };
                _linePaint.SetStyle(Paint.Style.Stroke);

                _pointPaint = new Paint(PaintFlags.AntiAlias)
                {
                    Color = Color.Rgb(248, 204, 27)
                };
                _pointPaint.SetStyle(Paint.Style.Fill);

                _axisPaint = new Paint(PaintFlags.AntiAlias)
                {
                    Color = Color.Rgb(148, 142, 123),
                    StrokeWidth = 2f
                };

                _labelPaint = new Paint(PaintFlags.AntiAlias)
                {
                    Color = Color.Rgb(232, 226, 208),
                    TextSize = 28f
                };

                _emptyPaint = new Paint(PaintFlags.AntiAlias)
                {
                    Color = Color.Rgb(202, 194, 174),
                    TextSize = 32f
                };
            }

            public void SetData(List<(DateTime date, double maxWeight)> points, string unit)
            {
                _points = points;
                _unit = unit;
                Invalidate();
            }

            protected override void OnDraw(Canvas canvas)
            {
                base.OnDraw(canvas);

                float left = 70f;
                float top = 30f;
                float right = Width - 20f;
                float bottom = Height - 50f;

                canvas.DrawLine(left, bottom, right, bottom, _axisPaint);
                canvas.DrawLine(left, top, left, bottom, _axisPaint);

                if (_points.Count == 0)
                {
                    canvas.DrawText("No data yet", left + 20f, top + 90f, _emptyPaint);
                    return;
                }

                if (_points.Count == 1)
                {
                    var point = _points[0];
                    canvas.DrawCircle((left + right) / 2f, (top + bottom) / 2f, 8f, _pointPaint);
                    canvas.DrawText($"{Math.Round(point.maxWeight, 1)} {_unit}", left + 20f, top + 90f, _labelPaint);
                    return;
                }

                double minWeight = _points.Min(p => p.maxWeight);
                double maxWeight = _points.Max(p => p.maxWeight);
                if (Math.Abs(maxWeight - minWeight) < 0.001)
                {
                    maxWeight += 1;
                    minWeight -= 1;
                }

                var path = new Android.Graphics.Path();
                for (int i = 0; i < _points.Count; i++)
                {
                    float x = left + ((right - left) * i / (_points.Count - 1));
                    float normalized = (float)((_points[i].maxWeight - minWeight) / (maxWeight - minWeight));
                    float y = bottom - normalized * (bottom - top);

                    if (i == 0)
                        path.MoveTo(x, y);
                    else
                        path.LineTo(x, y);

                    canvas.DrawCircle(x, y, 6f, _pointPaint);
                }

                canvas.DrawPath(path, _linePaint);
                canvas.DrawText($"Max {Math.Round(maxWeight, 1)} {_unit}", left + 10f, top + 24f, _labelPaint);
                canvas.DrawText($"Min {Math.Round(minWeight, 1)} {_unit}", left + 10f, bottom - 10f, _labelPaint);
            }
        }
    }
}
