using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Globalization;
using System.Diagnostics;

namespace WpfProgressbar
{
    public enum ProgressState
    {
        None = 0,
        Indeterminate = 1,
        Normal = 2,
        Completed = 3,
        Paused = 4
    }

    [TemplatePart(Name = "PART_Track", Type = typeof(FrameworkElement))]
    [TemplatePart(Name = "PART_Indicator", Type = typeof(FrameworkElement))]
    [TemplatePart(Name = "PART_GlowRect", Type = typeof(FrameworkElement))]
    [TemplatePart(Name = "PART_Stripe", Type = typeof(FrameworkElement))]
    public sealed class CustomProgressBar : RangeBase
    {
        #region Data

        private const string TrackTemplateName = "PART_Track";
        private const string IndicatorTemplateName = "PART_Indicator";
        private const string GlowingRectTemplateName = "PART_GlowRect";
        private const string StripeTemplateName = "PART_Stripe";

        private TranslateTransform _glowTransform = new TranslateTransform();
        private TranslateTransform _stripeTransform = new TranslateTransform();
        private ScaleTransform _indicatorTransform = new ScaleTransform(0, 1);

        private static Geometry _stripeGeometry = PathGeometry.Parse(
            "M 40 0 L 40 20 L 20 40 L 0 40 Z M 0 0 L 20 0 L 0 20 Z");
        private static DoubleAnimationBase _stripeAnimation;
        private AnimationClock _stripeAnimationClock;

        private static DoubleAnimationBase _completedAnimation;

        private FrameworkElement _track; // Currently not needed.
        private FrameworkElement _indicator;
        private FrameworkElement _glow;
        private FrameworkElement _stripe;

        #endregion Data

        #region Constructor

        // Override dependency properties
        static CustomProgressBar()
        {
            FocusableProperty.OverrideMetadata(typeof(CustomProgressBar), new FrameworkPropertyMetadata(false));

            // Set default to 100.0
            MaximumProperty.OverrideMetadata(typeof(CustomProgressBar), new FrameworkPropertyMetadata(100.0));

            // Set initial value of Foreground. Changing [Foreground] triggers re-creation of a glow brush.
            ForegroundProperty.OverrideMetadata(typeof(CustomProgressBar), new FrameworkPropertyMetadata(
                new SolidColorBrush(Color.FromArgb(255, 1, 140, 200)),
                (d, e) => { ((CustomProgressBar)d).SetGlowElementBrush(); }
            ){
                Inherits = false
            });

            // Set initial value of Background. Changing [Background] triggers re-creation of a stripe brush.
            BackgroundProperty.OverrideMetadata(typeof(CustomProgressBar), new FrameworkPropertyMetadata(
                new SolidColorBrush(Color.FromArgb(25, 127, 127, 127)),
                (d, e) => { ((CustomProgressBar)d).SetStripeElementBrush(); }
            ));

            BorderBrushProperty.OverrideMetadata(typeof(CustomProgressBar), new FrameworkPropertyMetadata(
                new SolidColorBrush(Color.FromArgb(255, 1, 90, 170))
            ));

            StyleProperty.OverrideMetadata(typeof(CustomProgressBar),
                new FrameworkPropertyMetadata(CreateStyle()));

            _stripeAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 40,
                Duration = new Duration(TimeSpan.FromMilliseconds(500)),
                FillBehavior = FillBehavior.HoldEnd,
                RepeatBehavior = RepeatBehavior.Forever
            };

            var anim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(1000),
            };
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0,
                KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0)))
            );
            anim.KeyFrames.Add(new SplineDoubleKeyFrame(1,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)))
            );
            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(1,
                KeyTime.FromTimeSpan(TimeSpan.FromSeconds(600)))
            );
            anim.KeyFrames.Add(new SplineDoubleKeyFrame(0.35,
                KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1000)))
            );
            _completedAnimation = anim;
        }

        public CustomProgressBar()
        {
            Margin = new Thickness { Left = 20, Right = 20 };
            VerticalAlignment = VerticalAlignment.Center;

            IsVisibleChanged += (s, e) => { UpdateAnimation(); };
            Loaded += (s, e) => {
                UpdateIndicator();
                UpdateAnimation();
            };

            // Prepare animation clock and controller
            _stripeAnimationClock = _stripeAnimation.CreateClock();
            _stripeTransform.ApplyAnimationClock(TranslateTransform.XProperty, _stripeAnimationClock, HandoffBehavior.Compose);
        }

        #endregion Constructor

        #region Feature

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            base.OnValueChanged(oldValue, newValue);

            if (newValue == 100.0)
            {
                SetCurrentValue(ProgressStateProperty, ProgressState.Completed);
            }

            UpdateIndicator();
        }

        // Set the width of the indicator
        private void UpdateIndicator()
        {
            if (_indicatorTransform != null)
            {
                double min = Minimum;
                double max = Maximum;
                double val = Value;

                // When maximum == minimum, have the indicator stretch the
                // whole length of track 
                double scale = max == min ? 1.0 : (val - min) / (max - min);

                _indicatorTransform.ScaleX = scale;
            }
        }

        // Switch the required animation state according to changes in [ProgressState]
        private static void ProgressStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CustomProgressBar source = (CustomProgressBar)d;

            source.SetGlowElementBrush();
            source.UpdateAnimation();
            source.UpdateCompletedBrush();
        }

        private void UpdateCompletedBrush()
        {
            // CompletedBrush fade-in animation
            if (_glow != null)
            {
                if (ProgressState == ProgressState.Completed)
                {
                    _glow.BeginAnimation(OpacityProperty, _completedAnimation);
                }
                else
                {
                    _glow.BeginAnimation(OpacityProperty, null);
                    _glow.Opacity = 1;
                }
            }
        }

        private void UpdateAnimation()
        {
            UpdateStripeAnimation();
            UpdateIndeterminateAnimation();
        }

        private bool _glowAnimating = false;
        private void UpdateIndeterminateAnimation(bool force = false)
        {
            if (!force && _glowAnimating) return;

            if (_glowTransform == null) {
                _glowAnimating = false;
                return;
            }

            if (IsVisible && IsIndeterminate && ActualWidth != 0)
            {
                _glowAnimating = true;
                var anim = new DoubleAnimation
                {
                    From = ActualWidth * -1,
                    To = ActualWidth * 2,
                    Duration = TimeSpan.FromMilliseconds(2000),
                };
                anim.Completed += (s, e) => {
                    UpdateIndeterminateAnimation(true);
                };
                _glowTransform.BeginAnimation(TranslateTransform.XProperty, anim, HandoffBehavior.SnapshotAndReplace);
            }
            else
            {
                _glowAnimating = false;
                _glowTransform.BeginAnimation(TranslateTransform.XProperty, null);
            }
        }

        private void UpdateStripeAnimation()
        {
            if (ProgressState == ProgressState.Normal && EnableStripeAnimation)
            {
                if (_stripeAnimationClock.IsPaused)
                    _stripeAnimationClock.Controller.Resume();
                else
                    _stripeAnimationClock.Controller.Begin();
            }
            else if (!_stripeAnimationClock.IsPaused)
            {
                _stripeAnimationClock.Controller.Pause();
            }
        }

        private void AnimateSmoothValue(double value)
        {
            var anim = new DoubleAnimation(value, TimeSpan.FromMilliseconds(500));
            BeginAnimation(ValueProperty, anim, HandoffBehavior.Compose);
        }

        // This is used to set the correct brush/opacity mask on the indicator. 
        private void SetGlowElementBrush()
        {
            if (_glow == null || !IsIndeterminate)
                return;

            _glow.InvalidateProperty(UIElement.OpacityMaskProperty);
            _glow.InvalidateProperty(Shape.FillProperty);

            if (Foreground is SolidColorBrush)
            {
                // Create the glow brush based on [Foreground]
                Color color = ((SolidColorBrush)this.Foreground).Color;
                var brush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0),
                    MappingMode = BrushMappingMode.RelativeToBoundingBox,
                    Transform = _glowTransform,
                    RelativeTransform = new ScaleTransform(1.0, 1.0)
                };

                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 0.0));
                brush.GradientStops.Add(new GradientStop(color, 0.4));
                brush.GradientStops.Add(new GradientStop(color, 0.6));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1.0));
                _glow.SetCurrentValue(Shape.FillProperty, brush);
            }
            else
            {
                // Foreground is not a SolidColorBrush, use an opacity mask. 
                LinearGradientBrush mask = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0),
                    MappingMode = BrushMappingMode.RelativeToBoundingBox,
                    Transform = _glowTransform,
                    RelativeTransform = new ScaleTransform(1.0, 1.0)
                };
                mask.GradientStops.Add(new GradientStop(Colors.Transparent, 0.0));
                mask.GradientStops.Add(new GradientStop(Colors.Black, 0.4));
                mask.GradientStops.Add(new GradientStop(Colors.Black, 0.6));
                mask.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
                _glow.SetCurrentValue(UIElement.OpacityMaskProperty, mask);
                _glow.SetCurrentValue(Shape.FillProperty, this.Foreground);
            }
        }

        // This is used to set the correct brush/opacity mask on the stripe. 
        private void SetStripeElementBrush()
        {
            if (_stripe == null)
                return;

            _stripe.InvalidateProperty(UIElement.OpacityMaskProperty);
            _stripe.InvalidateProperty(Shape.FillProperty);

            var geometryDrawing = new GeometryDrawing
            {
                Geometry = _stripeGeometry
            };

            if (Background is SolidColorBrush)
            {
                // Create the stripe brush based on [Background]
                Color basis = ((SolidColorBrush)Background).Color;
                Color color;
                if (basis.R + basis.G + basis.B > 383)
                    color = Color.FromArgb(basis.A, (byte)(basis.R * 0.9), (byte)(basis.G * 0.9), (byte)(basis.B * 0.9));
                else
                    color = Color.FromArgb(basis.A, (byte)(basis.R * 1.1), (byte)(basis.G * 1.1), (byte)(basis.B * 1.1));

                geometryDrawing.Brush = new SolidColorBrush(color);
            }
            else
            {
                geometryDrawing.Brush = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0));
            }

            var stripeBrush = new DrawingBrush
            {
                TileMode = TileMode.Tile,
                Stretch = Stretch.None,
                Viewport = new Rect(0, 0, 40, 40),
                ViewportUnits = BrushMappingMode.Absolute,
                Transform = _stripeTransform,
                Drawing = geometryDrawing
            };

            _stripe.SetCurrentValue(Shape.FillProperty, stripeBrush);
        }

        #endregion Feature

        private static Style CreateStyle()
        {
            var border = new FrameworkElementFactory(typeof(Border), "Border");
            border.SetValue(BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
            border.SetValue(BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));

            var percentBinding = new MultiBinding
            {
                Converter = PercentageConverter.I,
                StringFormat = "{0:N1} %"
            };
            percentBinding.Bindings.Add(new Binding("Minimum") { RelativeSource = RelativeSource.TemplatedParent });
            percentBinding.Bindings.Add(new Binding("Maximum") { RelativeSource = RelativeSource.TemplatedParent });
            percentBinding.Bindings.Add(new Binding("Value") { RelativeSource = RelativeSource.TemplatedParent });

            var percentText = new FrameworkElementFactory(typeof(TextBlock), "Percent");
            percentText.SetBinding(TextBlock.TextProperty, percentBinding);
            percentText.SetValue(MarginProperty, new Thickness { Right = 20 });
            percentText.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Right);
            percentText.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            percentText.SetValue(ForegroundProperty, new TemplateBindingExtension(TextBrushProperty));

            var progressLabel = new FrameworkElementFactory(typeof(TextBlock), "Label");
            progressLabel.SetValue(TextBlock.TextProperty, new TemplateBindingExtension(LabelTextProperty));
            progressLabel.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
            progressLabel.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            progressLabel.SetValue(ForegroundProperty, new TemplateBindingExtension(TextBrushProperty));
            progressLabel.SetValue(MarginProperty, new Thickness { Left = 10.0 });

            var partGlowRect = new FrameworkElementFactory(typeof(Rectangle), GlowingRectTemplateName);
            partGlowRect.SetValue(VisibilityProperty, Visibility.Collapsed);

            var partIndicator = new FrameworkElementFactory(typeof(Rectangle), IndicatorTemplateName);
            partIndicator.SetValue(Shape.FillProperty, new TemplateBindingExtension(ForegroundProperty));

            var partTrack = new FrameworkElementFactory(typeof(Rectangle), TrackTemplateName);
            partTrack.SetValue(Shape.FillProperty, new TemplateBindingExtension(BackgroundProperty));

            var partStripe = new FrameworkElementFactory(typeof(Rectangle), StripeTemplateName);
            partStripe.SetValue(VisibilityProperty, Visibility.Collapsed);

            var root = new FrameworkElementFactory(typeof(Grid), "TemplateRoot");
            root.SetValue(MinHeightProperty, 14.0);
            root.SetValue(MinWidthProperty, 20.0);
            root.AppendChild(partTrack);
            root.AppendChild(partStripe);
            root.AppendChild(partIndicator);
            root.AppendChild(partGlowRect);
            root.AppendChild(percentText);
            root.AppendChild(progressLabel);
            root.AppendChild(border);

            var isIndeterminate = new Trigger{ Property = ProgressStateProperty, Value = ProgressState.Indeterminate };
            isIndeterminate.Setters.Add(new Setter(VisibilityProperty, Visibility.Collapsed, "Percent"));
            isIndeterminate.Setters.Add(new Setter(VisibilityProperty, Visibility.Collapsed, IndicatorTemplateName));
            isIndeterminate.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, GlowingRectTemplateName));

            var stripeAnimationTrigger = new MultiTrigger();
            stripeAnimationTrigger.Conditions.Add(new Condition(ProgressStateProperty, ProgressState.Normal));
            stripeAnimationTrigger.Conditions.Add(new Condition(EnableStripeAnimationProperty, true));
            stripeAnimationTrigger.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, StripeTemplateName));

            var pausedTrigger = new MultiTrigger();
            pausedTrigger.Conditions.Add(new Condition(ProgressStateProperty, ProgressState.Paused));
            pausedTrigger.Conditions.Add(new Condition(EnableStripeAnimationProperty, true));
            pausedTrigger.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, StripeTemplateName));

            var completedTrigger = new Trigger{ Property = ProgressStateProperty, Value = ProgressState.Completed };
            completedTrigger.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, GlowingRectTemplateName));
            completedTrigger.Setters.Add(new Setter(Shape.FillProperty,
                new Binding("CompletedBrush") { RelativeSource = RelativeSource.TemplatedParent }, GlowingRectTemplateName));

            var ct = new ControlTemplate(typeof(RangeBase))
            {
                VisualTree = root
            };
            ct.Triggers.Add(isIndeterminate);
            ct.Triggers.Add(stripeAnimationTrigger);
            ct.Triggers.Add(pausedTrigger);
            ct.Triggers.Add(completedTrigger);

            var style = new Style(typeof(RangeBase));
            style.Setters.Add(new Setter(TemplateProperty, ct));
            style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(1.0)));
            style.Setters.Add(new Setter(HeightProperty, 20.0));
            style.Setters.Add(new Setter(SnapsToDevicePixelsProperty, true));
            return style;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var track = GetTemplateChild(TrackTemplateName);
            if (track != null)
                _track = (FrameworkElement)track;

            var glow = GetTemplateChild(GlowingRectTemplateName);
            if (glow != null)
                _glow = (FrameworkElement)glow;

            var stripe = GetTemplateChild(StripeTemplateName);
            if (stripe != null)
                _stripe = (FrameworkElement)stripe;

            var indicator = GetTemplateChild(IndicatorTemplateName);
            if (indicator != null)
            {
                _indicator = (FrameworkElement)indicator;
                _indicator.InvalidateProperty(RenderTransformProperty);
                _indicator.RenderTransform = _indicatorTransform;
            }

            SetGlowElementBrush();
            SetStripeElementBrush();
        }

        protected override void OnMinimumChanged(double oldMinimum, double newMinimum)
        {
            base.OnMinimumChanged(oldMinimum, newMinimum);
            UpdateIndicator();
        }

        protected override void OnMaximumChanged(double oldMaximum, double newMaximum)
        {
            base.OnMaximumChanged(oldMaximum, newMaximum);
            UpdateIndicator();
        }


        #region DependencyProperties

        public double SmoothValue
        {
            get { return (double)GetValue(SmoothValueProperty); }
            set { SetValue(SmoothValueProperty, value); }
        }

        public static readonly DependencyProperty SmoothValueProperty =
            DependencyProperty.Register("SmoothValue",
                typeof(double), typeof(CustomProgressBar), new PropertyMetadata(
                    (d, e) => { ((CustomProgressBar)d).AnimateSmoothValue((double)e.NewValue); }));

        public string LabelText
        {
            get { return (string)GetValue(LabelTextProperty); }
            set { SetValue(LabelTextProperty, value); }
        }

        public static readonly DependencyProperty LabelTextProperty =
            DependencyProperty.Register("LabelText",
                typeof(string), typeof(CustomProgressBar), new PropertyMetadata(String.Empty));


        public ProgressState ProgressState
        {
            get { return (ProgressState)GetValue(ProgressStateProperty); }
            set { SetValue(ProgressStateProperty, value); }
        }
        public static readonly DependencyProperty ProgressStateProperty =
            DependencyProperty.Register("ProgressState",
                typeof(ProgressState), typeof(CustomProgressBar), new PropertyMetadata(
                    ProgressState.Indeterminate, new PropertyChangedCallback(ProgressStateChanged)));

        public bool EnableStripeAnimation
        {
            get { return (bool)GetValue(EnableStripeAnimationProperty); }
            set { SetValue(EnableStripeAnimationProperty, value); }
        }
        public static readonly DependencyProperty EnableStripeAnimationProperty =
            DependencyProperty.Register("EnableStripeAnimation",
                typeof(bool), typeof(CustomProgressBar), new PropertyMetadata(true, new PropertyChangedCallback(ProgressStateChanged)));

        public Brush CompletedBrush
        {
            get { return (Brush)GetValue(CompletedBrushProperty); }
            set { SetValue(CompletedBrushProperty, value); }
        }
        public static readonly DependencyProperty CompletedBrushProperty =
            DependencyProperty.Register("CompletedBrush", typeof(Brush), typeof(CustomProgressBar),
                                        new PropertyMetadata(new SolidColorBrush(Color.FromArgb(255, 20, 255, 255))));

        public Brush TextBrush
        {
            get { return (Brush)GetValue(TextBrushProperty); }
            set { SetValue(TextBrushProperty, value); }
        }
        public static readonly DependencyProperty TextBrushProperty =
            DependencyProperty.Register("TextBrush", typeof(Brush), typeof(CustomProgressBar),
                                        new PropertyMetadata(System.Windows.SystemColors.HighlightTextBrush));

        #endregion DependencyProperties

        public bool IsIndeterminate
        {
            get { return ProgressState == ProgressState.Indeterminate; }
        }

        private class PercentageConverter : IMultiValueConverter
        {
            public static PercentageConverter I = new PercentageConverter();
            public object Convert(object[] value, Type type, object parameter, CultureInfo culture)
            {
                object result;
                try
                {
                    var minimum = (double)value[0];
                    var maximum = (double)value[1];
                    var val = (double)value[2];
                    result = (maximum == minimum) ? 100.0 : 100.0 * (val - minimum) / (maximum - minimum) ;
                }
                catch
                {
                    result = Binding.DoNothing;
                }
                return result;
            }

            public object[] ConvertBack(object value, Type[] type, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
    }
}
