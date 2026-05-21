using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace Gym_App.Views
{
    /// <summary>
    /// MainActivity root: system-bar insets plus extra spacing for welcome hero and bottom buttons.
    /// </summary>
    public class InsetsAwareWelcomeRoot : LinearLayout
    {
        private int _basePaddingLeft;
        private int _basePaddingTop;
        private int _basePaddingRight;
        private int _basePaddingBottom;
        private int _extraTopPadding;
        private int _extraBottomPadding;
        private int _minBottomGesturePadding;

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
            _extraBottomPadding = (int)Resources.GetDimension(Resource.Dimension.welcome_screen_bottom_extra);
            _minBottomGesturePadding = (int)Resources.GetDimension(Resource.Dimension.welcome_screen_bottom_gesture_min);

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                SetOnApplyWindowInsetsListener(new WelcomeInsetsListener(this));
            }

            RequestApplyInsets();
        }

        protected override void OnDetachedFromWindow()
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                SetOnApplyWindowInsetsListener(null);
            }

            base.OnDetachedFromWindow();
        }

        public override WindowInsets? OnApplyWindowInsets(WindowInsets? insets)
        {
            if (insets == null)
            {
                return base.OnApplyWindowInsets(insets);
            }

            return ApplyInsetsAndConsume(insets);
        }

        internal WindowInsets? ApplyInsetsAndConsume(WindowInsets insets)
        {
            var (topInset, bottomInset) = ResolveVerticalInsets(insets);
            SetPadding(
                _basePaddingLeft,
                _basePaddingTop + topInset + _extraTopPadding,
                _basePaddingRight,
                _basePaddingBottom + bottomInset + _extraBottomPadding);

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var consumed = Insets.Of(0, topInset + _extraTopPadding, 0, bottomInset + _extraBottomPadding);
                return insets.Inset(consumed);
            }

            return insets;
        }

        private (int Top, int Bottom) ResolveVerticalInsets(WindowInsets insets)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var systemBars = insets.GetInsets(WindowInsets.Type.SystemBars());
                var navigationBars = insets.GetInsets(WindowInsets.Type.NavigationBars());
                var cutout = insets.GetInsets(WindowInsets.Type.DisplayCutout());
                var gestures = insets.GetInsets(WindowInsets.Type.SystemGestures());
                var mandatoryGestures = insets.GetInsets(WindowInsets.Type.MandatorySystemGestures());

                var top = Math.Max(systemBars.Top, cutout.Top);
                var bottom = Math.Max(
                    Math.Max(systemBars.Bottom, navigationBars.Bottom),
                    Math.Max(gestures.Bottom, mandatoryGestures.Bottom));
                bottom = Math.Max(bottom, _minBottomGesturePadding);
                return (top, bottom);
            }

#pragma warning disable CS0618
            var legacyTop = insets.SystemWindowInsetTop;
            var legacyBottom = Math.Max(insets.SystemWindowInsetBottom, _minBottomGesturePadding);
#pragma warning restore CS0618
            return (legacyTop, legacyBottom);
        }

        private sealed class WelcomeInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
        {
            private readonly InsetsAwareWelcomeRoot _root;

            public WelcomeInsetsListener(InsetsAwareWelcomeRoot root)
            {
                _root = root;
            }

            public WindowInsets OnApplyWindowInsets(View? v, WindowInsets insets)
            {
                return _root.ApplyInsetsAndConsume(insets);
            }
        }
    }
}
