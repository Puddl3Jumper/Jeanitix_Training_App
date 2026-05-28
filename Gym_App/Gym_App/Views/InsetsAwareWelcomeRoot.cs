using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace Gym_App.Views
{
    /// <summary>
    /// MainActivity root: extra top spacing for hero; bottom safe area on the button bar.
    /// </summary>
    public class InsetsAwareWelcomeRoot : LinearLayout
    {
        private int _basePaddingLeft;
        private int _basePaddingTop;
        private int _basePaddingRight;
        private int _baseRootPaddingBottom;
        private int _baseBottomBarPaddingBottom;
        private int _extraTopPadding;
        private int _extraBottomPadding;
        private int _minBottomGesturePadding;
        private LinearLayout? _bottomBar;

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

        protected override void OnFinishInflate()
        {
            base.OnFinishInflate();
            _bottomBar = FindViewById<LinearLayout>(Resource.Id.welcomeBottomBar);
        }

        protected override void OnAttachedToWindow()
        {
            base.OnAttachedToWindow();
            _basePaddingLeft = PaddingLeft;
            _basePaddingTop = PaddingTop;
            _basePaddingRight = PaddingRight;
            _baseRootPaddingBottom = PaddingBottom;
            _extraTopPadding = (int)Resources.GetDimension(Resource.Dimension.welcome_screen_top_extra);
            _extraBottomPadding = (int)Resources.GetDimension(Resource.Dimension.welcome_screen_bottom_extra);
            _minBottomGesturePadding = (int)Resources.GetDimension(Resource.Dimension.welcome_screen_bottom_gesture_min);

            if (_bottomBar != null)
            {
                _baseBottomBarPaddingBottom = _bottomBar.PaddingBottom;
            }

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

        internal WindowInsets ApplyInsetsAndConsume(WindowInsets insets)
        {
            var (topInset, bottomInset) = ResolveVerticalInsets(insets);

            SetPadding(
                _basePaddingLeft,
                _basePaddingTop + topInset + _extraTopPadding,
                _basePaddingRight,
                _baseRootPaddingBottom);

            if (_bottomBar != null)
            {
                var bottomBarPadding = _baseBottomBarPaddingBottom + ResolveBottomBarPadding(bottomInset);
                _bottomBar.SetPadding(
                    _bottomBar.PaddingLeft,
                    _bottomBar.PaddingTop,
                    _bottomBar.PaddingRight,
                    bottomBarPadding);
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var consumed = Insets.Of(0, topInset + _extraTopPadding, 0, bottomInset + _extraBottomPadding);
                return insets.Inset(consumed);
            }

            return insets;
        }

        private int ResolveBottomBarPadding(int bottomInset)
        {
            if (bottomInset > 0)
            {
                return bottomInset + _extraBottomPadding;
            }

            return _minBottomGesturePadding + _extraBottomPadding;
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
                return (top, bottom);
            }

#pragma warning disable CS0618
            var legacyTop = insets.SystemWindowInsetTop;
            var legacyBottom = insets.SystemWindowInsetBottom;
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
