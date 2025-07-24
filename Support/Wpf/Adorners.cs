using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows;
using System.Xml.Linq;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Reflection;

namespace Support.Wpf
{
    public static class Behavior
    {

        public static readonly DependencyProperty IsBlinkingProperty =
            DependencyProperty.RegisterAttached(
                "IsBlinking",
                typeof(bool),
                typeof(Behavior),
                new PropertyMetadata(false, OnIsBlinkingChanged));

        public static readonly DependencyProperty BlinkColorProperty =
            DependencyProperty.RegisterAttached(
                "BlinkColor",
                typeof(Color),
                typeof(Behavior),
                new PropertyMetadata(Colors.Red, OnBlinkColorChanged));

        private static void OnIsBlinkingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uiElement = d as FrameworkElement;
            if (uiElement == null)
                return;

            if ((bool)e.NewValue)
            {
                if (AdornerLayer.GetAdornerLayer(uiElement) != null)
                    StartBlinking(uiElement);
                else
                {
                    uiElement.Loaded += (sender, args) => {
                        StartBlinking(uiElement);
                    };
                }
            }
            else
                StopBlinking(uiElement);
        }
        private static void OnBlinkColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uiElement = d as FrameworkElement;
            if (d == null) return;
            if (AdornerLayer.GetAdornerLayer(uiElement) == null)
            {
                uiElement!.Loaded += (sender, args) =>
                {
                    var adorner = GetBlinkingAdorner(d);
                    if (adorner != null)
                    {
                        adorner.BlinkColor = (Color)e.NewValue;
                        adorner?.UpdateBlinkColor();
                    }
                };
            }
            else
            {
                var adorner = GetBlinkingAdorner(d);
                if (adorner != null)
                {
                    adorner.BlinkColor = (Color)e.NewValue;
                    adorner.UpdateBlinkColor();
                }
            }
        }

        private static void StartBlinking(UIElement uiElement)
        {
            var adornerLayer = AdornerLayer.GetAdornerLayer(uiElement);
            if (adornerLayer == null)
                return;

            var adorner = GetBlinkingAdorner(uiElement);
            if (adorner == null)
            {
                var blinkColor = GetBlinkColor(uiElement);
                _ = new BlinkingAdorner(uiElement, blinkColor);
            }
            else
                adorner.UpdateBlinkColor(); 
        }
        private static void StopBlinking(UIElement uiElement)
        {
            var adorner = GetBlinkingAdorner(uiElement);
            adorner?.StopBlinking();
            adorner?.UpdateBlinkColor();
        }

        public static void SetIsBlinking(UIElement element, bool value)
        {
            element.SetValue(IsBlinkingProperty, value);
        }

        public static bool GetIsBlinking(UIElement element)
        {
            return (bool)element.GetValue(IsBlinkingProperty);
        }

        public static void SetBlinkColor(UIElement element, Color value)
        {
            element.SetValue(BlinkColorProperty, value);
        }

        public static Color GetBlinkColor(UIElement element)
        {
            return (Color)element.GetValue(BlinkColorProperty);
        }
        private static BlinkingAdorner? GetBlinkingAdorner(DependencyObject d)
        {
            var uiElement = d as UIElement;
            if (uiElement == null)
                return null;

            var adornerLayer = AdornerLayer.GetAdornerLayer(uiElement);
            var adorners = adornerLayer?.GetAdorners(uiElement);

            return adorners?.OfType<BlinkingAdorner>().FirstOrDefault();
        }
    }

    public class BlinkingAdorner : Adorner
    {
        private readonly DoubleAnimation animation;

        public Color? OrigColor { get; set; }
        public Color BlinkColor { get; set; }

        public void UpdateBlinkColor()
        {
            PropertyInfo foregroundProperty = AdornedElement.GetType().GetProperty("Foreground")!;
            if (foregroundProperty != null && foregroundProperty.CanWrite)
            {
                OrigColor = (foregroundProperty.GetValue(AdornedElement) as SolidColorBrush)?.Color;
                foregroundProperty.SetValue(AdornedElement, new SolidColorBrush(BlinkColor));
            }

            PropertyInfo fillProperty = AdornedElement.GetType().GetProperty("Fill")!;
            if (fillProperty != null && fillProperty.CanWrite)
            {
                OrigColor = (fillProperty.GetValue(AdornedElement) as SolidColorBrush)?.Color;
                fillProperty.SetValue(AdornedElement, new SolidColorBrush(BlinkColor));
            }

            PropertyInfo borderProperty = AdornedElement.GetType().GetProperty("BorderBrush")!;
            if (borderProperty != null && borderProperty.CanWrite)
            {
                OrigColor = (borderProperty.GetValue(AdornedElement) as SolidColorBrush)?.Color;
                borderProperty.SetValue(AdornedElement, new SolidColorBrush(BlinkColor));
            }
        }

        private readonly AdornerLayer adornerLayer;
        Storyboard storyBoard = new Storyboard();

        public BlinkingAdorner(UIElement adornedElement, Color blinkColor) : base(adornedElement)
        {
            BlinkColor = blinkColor;
            UpdateBlinkColor();
            adornerLayer = AdornerLayer.GetAdornerLayer(adornedElement);
            adornerLayer.Add(this);

            animation = new DoubleAnimation
            {
                From = 1,
                To = 0.5,
                AutoReverse = true,
                Duration = new Duration(TimeSpan.FromSeconds(0.4)),
                RepeatBehavior = RepeatBehavior.Forever
            };

            storyBoard.Children.Add(animation);
            Storyboard.SetTarget(animation, adornedElement);
            Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.OpacityProperty));
            storyBoard.Begin();
        }

        protected override int VisualChildrenCount => 0;

        public void StopBlinking()
        {
            storyBoard.Stop();
            if(OrigColor!= null)
            {
                BlinkColor = OrigColor.Value;
                UpdateBlinkColor();
            }
        }
        public void RemoveFromAdornerLayer()
        {
            var adornerLayer = AdornerLayer.GetAdornerLayer(AdornedElement);
            if (adornerLayer != null)
                adornerLayer.Remove(this);
        }
        protected override Visual? GetVisualChild(int index)
        {
            return null;
        }
    }
}
