using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using System;

namespace Gym_App.Views
{
    public class InsetsAwareBottomBar : LinearLayout
    {
        private int _baseBottomPadding;
        private int _extraBottomPadding;

        public InsetsAwareBottomBar(Context context) : base(context)
        {
        }

        public InsetsAwareBottomBar(Context context, IAttributeSet attrs) : base(context, attrs)
        {
        }

        public InsetsAwareBottomBar(Context context, IAttributeSet attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
        }

        protected override void OnAttachedToWindow()
        {
            base.OnAttachedToWindow();
            _baseBottomPadding = PaddingBottom;
            _extraBottomPadding = (int)(6f * Resources.DisplayMetrics.Density + 0.5f);
            RequestApplyInsets();
        }

        public override WindowInsets? OnApplyWindowInsets(WindowInsets? insets)
        {
            if (insets == null)
            {
                return base.OnApplyWindowInsets(insets);
            }

            int bottomInset;
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                bottomInset = insets.GetInsets(WindowInsets.Type.SystemBars()).Bottom;
            }
            else
            {
#pragma warning disable CS0618
                bottomInset = insets.SystemWindowInsetBottom;
#pragma warning restore CS0618
            }

            SetPadding(PaddingLeft, PaddingTop, PaddingRight, _baseBottomPadding + bottomInset + _extraBottomPadding);
            return base.OnApplyWindowInsets(insets);
        }
    }
}
