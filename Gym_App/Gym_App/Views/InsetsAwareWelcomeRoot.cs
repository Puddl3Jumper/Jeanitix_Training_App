using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace Gym_App.Views
{
    /// <summary>
    /// MainActivity root: status-bar + cutout insets plus extra top spacing for the welcome hero.
    /// </summary>
    public class InsetsAwareWelcomeRoot : LinearLayout
    {
        private int _basePaddingLeft;
        private int _basePaddingTop;
        private int _basePaddingRight;
        private int _basePaddingBottom;
        private int _extraTopPadding;

        public InsetsAwareWelcomeRoot(Context context) : base(context)
        {
        }

        public InsetsAwareWelcomeRoot(Context context, IAttributeSet attrs) : base(context, attrs)
        {
        }

        public InsetsAwareWelcomeRoot(Context context, IAttributeSet attrs, int defStyleAttr)
            : base(context, attrs, defStyleAttr)
        {
        }

        protected override void OnAttachedToWindow()
        {
            base.OnAttachedToWindow();
            _basePaddingLeft = PaddingLeft;
            _basePaddingTop = PaddingTop;
            _basePaddingRight = PaddingRight;
            _basePaddingBottom = PaddingBottom;
            _extraTopPadding = (int)Resources.GetDimension(Resource.Dimension.welcome_screen_top_extra);
            RequestApplyInsets();
        }

        public override WindowInsets? OnApplyWindowInsets(WindowInsets? insets)
        {
            if (insets == null)
            {
                return base.OnApplyWindowInsets(insets);
            }

            int topInset;
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var systemBars = insets.GetInsets(WindowInsets.Type.SystemBars());
                var cutout = insets.GetInsets(WindowInsets.Type.DisplayCutout());
                topInset = Math.Max(systemBars.Top, cutout.Top);
            }
            else
            {
#pragma warning disable CS0618
                topInset = insets.SystemWindowInsetTop;
#pragma warning restore CS0618
            }

            var top = _basePaddingTop + topInset + _extraTopPadding;
            SetPadding(_basePaddingLeft, top, _basePaddingRight, _basePaddingBottom);
            return insets;
        }
    }
}
