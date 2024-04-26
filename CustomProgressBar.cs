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
using System.Reflection.Metadata;
using System.Text.Json;
using System.Reflection;
using Microsoft.Windows.Themes;
using System.Data;
using System.Diagnostics;
using System.Globalization;

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
        private FrameworkElement? _track;
        private FrameworkElement? _indicator;
        private FrameworkElement? _glow;
        private FrameworkElement? _stripe;

        #endregion Data

        #region Constructor

        // Override dependency properties
        static CustomProgressBar()
        {
            FocusableProperty.OverrideMetadata(typeof(CustomProgressBar), new FrameworkPropertyMetadata(false));

            // Set default to 100.0
            MaximumProperty.OverrideMetadata(typeof(CustomProgressBar), new FrameworkPropertyMetadata(100.0));

            ForegroundProperty.OverrideMetadata(typeof(CustomProgressBar), new FrameworkPropertyMetadata(
                new SolidColorBrush(Color.FromArgb(255, 1, 140, 200)),
                (d, e) => { ((CustomProgressBar)d).SetGlowElementBrush(); }
            ){
                Inherits = false
            });

            BackgroundProperty.OverrideMetadata(typeof(CustomProgressBar), new FrameworkPropertyMetadata(
                new SolidColorBrush(Color.FromArgb(25, 127, 127, 127)),
                (d, e) => { ((CustomProgressBar)d).SetStripeElementBrush(); }
            ));

            BorderBrushProperty.OverrideMetadata(typeof(CustomProgressBar), new FrameworkPropertyMetadata(
                new SolidColorBrush(Color.FromArgb(255, 1, 90, 170))
            ));

            StyleProperty.OverrideMetadata(typeof(CustomProgressBar),
                new FrameworkPropertyMetadata(CreateStyle()));
        }

        public CustomProgressBar()
        {
            Margin = new Thickness { Left = 20, Right = 20 };
            VerticalAlignment = VerticalAlignment.Center;

            IsVisibleChanged += (s, e) => { UpdateAnimation(); };
            Loaded += (s, e) => { UpdateIndicator(); };
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
            if (_track != null && _indicator != null)
            {
                double min = Minimum;
                double max = Maximum;
                double val = Value;

                // When maximum <= minimum, have the indicator stretch the
                // whole length of track 
                double percent = max <= min ? 1.0 : (val - min) / (max - min);

                _indicatorTransform.ScaleX = percent;
            }
        }

        // Switch the required animation state according to changes in [ProgressState]
        private static void ProgressStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CustomProgressBar source = (CustomProgressBar)d;
            source.UpdateAnimation();
            source.SetGlowElementBrush();
        }

        private void UpdateAnimation()
        {
            UpdateStripeAnimation();
            UpdateIndeterminateAnimation();

            // CompletedBrush fade-in animation
            if (_glow != null)
            {
                if (ProgressState == ProgressState.Completed)
                {
                    var anim = new DoubleAnimationUsingKeyFrames
                    {
                        FillBehavior = FillBehavior.HoldEnd,
                        Duration = TimeSpan.FromMilliseconds(800),
                    };
                    anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0,
                        KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0)))
                    );
                    anim.KeyFrames.Add(new SplineDoubleKeyFrame(1,
                        KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)))
                    );
                    anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(1,
                        KeyTime.FromTimeSpan(TimeSpan.FromSeconds(500)))
                    );
                    anim.KeyFrames.Add(new SplineDoubleKeyFrame(0.5,
                        KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(800)))
                    );
                    _glow.BeginAnimation(OpacityProperty, anim);
                }
                else
                {
                    _glow.BeginAnimation(OpacityProperty, null);
                    _glow.Opacity = 1;
                }
            }
        }

        private bool _glowAnimating = false;
        private void UpdateIndeterminateAnimation(bool force = false)
        {
            if (_glow == null || _glowTransform == null) return;

            if (!force && _glowAnimating) return;

            if (IsVisible && ProgressState == ProgressState.Indeterminate)
            {
                _glowAnimating = true;
                var anim = new DoubleAnimation
                {
                    From = ActualWidth * -1,
                    To = ActualWidth * 2,
                    Duration = TimeSpan.FromMilliseconds(2000),
                };
                anim.Completed += (s, e) => {
                    if (IsIndeterminate)
                        UpdateIndeterminateAnimation(true);
                    else
                        _glowAnimating = false;
                };
                _glowTransform.BeginAnimation(TranslateTransform.XProperty, anim);
            }
            else
            {
                _glowTransform.BeginAnimation(TranslateTransform.XProperty, null);
            }
        }

        private void UpdateStripeAnimation()
        {
            if (ProgressState == ProgressState.Normal && EnableStripeAnimation)
            {
                var anim = new DoubleAnimation
                {
                    From = 0.0,
                    To = 40,
                    RepeatBehavior = RepeatBehavior.Forever,
                    Duration = new Duration(TimeSpan.FromMilliseconds(500))
                };
                _stripeTransform.BeginAnimation(TranslateTransform.XProperty, anim, HandoffBehavior.Compose);
            }
            else
            {
                var anim = new DoubleAnimation
                {
                    By = 10,
                    EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut },
                    Duration = new Duration(TimeSpan.FromMilliseconds(500)),
                    FillBehavior = FillBehavior.HoldEnd
                };
                _stripeTransform.BeginAnimation(TranslateTransform.XProperty, anim, HandoffBehavior.SnapshotAndReplace);
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

            if (this.Foreground is SolidColorBrush)
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
                Geometry = PathGeometry.Parse("M 40 0 L 40 20 L 20 40 L 0 40 Z M 0 0 L 20 0 L 0 20 Z")
            };

            var stripeBrush = new DrawingBrush
            {
                TileMode = TileMode.Tile,
                Stretch = Stretch.None,
                Viewport = new Rect(0, 0, 40, 40),
                ViewportUnits = BrushMappingMode.Absolute,
                Transform = _stripeTransform,
                Drawing = geometryDrawing
            };

            if (this.Background is SolidColorBrush)
            {
                // Create the stripe brush based on [Background]
                Color basis = ((SolidColorBrush)this.Background).Color;
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

            stripeBrush.Drawing = geometryDrawing;
            _stripe.SetCurrentValue(Shape.FillProperty, stripeBrush);
        }

        #endregion Feature

        private static Style CreateStyle()
        {
            var border = new FrameworkElementFactory(typeof(Border), "Border");
            border.SetValue(BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
            border.SetValue(BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));

            var percentText = new FrameworkElementFactory(typeof(TextBlock), "Percent");
            percentText.SetBinding(TextBlock.TextProperty, new Binding("Value")
            {
                RelativeSource = RelativeSource.TemplatedParent,
                StringFormat = "{0:N1} %"
            });
            percentText.SetValue(MarginProperty, new Thickness { Right = 20 });
            percentText.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Right);
            percentText.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            percentText.SetValue(ForegroundProperty, System.Windows.SystemColors.HighlightTextBrush);  // TODO change TemplateBinding

            var progressLabel = new FrameworkElementFactory(typeof(TextBlock), "Label");
            progressLabel.SetValue(TextBlock.TextProperty, new TemplateBindingExtension(LabelTextProperty));
            progressLabel.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
            progressLabel.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            progressLabel.SetValue(ForegroundProperty, System.Windows.SystemColors.HighlightTextBrush); // TODO change TemplateBinding
            progressLabel.SetValue(MarginProperty, new Thickness { Left = 10.0 });

            var partGlowRect = new FrameworkElementFactory(typeof(Rectangle), "PART_GlowRect");
            partGlowRect.SetValue(VisibilityProperty, Visibility.Collapsed);

            var partIndicator = new FrameworkElementFactory(typeof(Rectangle), "PART_Indicator");
            partIndicator.SetValue(Shape.FillProperty, new TemplateBindingExtension(ForegroundProperty));

            var partTrack = new FrameworkElementFactory(typeof(Rectangle), "PART_Track");
            partTrack.SetValue(Shape.FillProperty, new TemplateBindingExtension(BackgroundProperty));

            var partStripe = new FrameworkElementFactory(typeof(Rectangle), "PART_Stripe");
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
            isIndeterminate.Setters.Add(new Setter(VisibilityProperty, Visibility.Collapsed, "PART_Indicator"));
            isIndeterminate.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "PART_GlowRect"));

            var stripeAnimationTrigger = new MultiTrigger();
            stripeAnimationTrigger.Conditions.Add(new Condition(ProgressStateProperty, ProgressState.Normal));
            stripeAnimationTrigger.Conditions.Add(new Condition(EnableStripeAnimationProperty, true));
            stripeAnimationTrigger.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "PART_Stripe"));

            var pausedTrigger = new MultiTrigger();
            pausedTrigger.Conditions.Add(new Condition(ProgressStateProperty, ProgressState.Paused));
            pausedTrigger.Conditions.Add(new Condition(EnableStripeAnimationProperty, true));
            pausedTrigger.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "PART_Stripe"));

            var completedTrigger = new Trigger{ Property = ProgressStateProperty, Value = ProgressState.Completed };
            completedTrigger.Setters.Add(new Setter(VisibilityProperty, Visibility.Visible, "PART_GlowRect"));
            completedTrigger.Setters.Add(new Setter(Shape.FillProperty,
                new Binding("CompletedBrush") { RelativeSource = RelativeSource.TemplatedParent }, "PART_GlowRect"));

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

            _track = GetTemplateChild(TrackTemplateName) as FrameworkElement;
            _indicator = GetTemplateChild(IndicatorTemplateName) as FrameworkElement;
            _glow = GetTemplateChild(GlowingRectTemplateName) as FrameworkElement;
            _stripe = GetTemplateChild(StripeTemplateName) as FrameworkElement;

            if (_indicator != null)
            {
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

        #endregion DependencyProperties

        public bool IsIndeterminate
        {
            get { return ProgressState == ProgressState.Indeterminate; }
        }
    }
}
