using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Tobi.Infrastructure;
using Colors=System.Windows.Media.Colors;

namespace Tobi.Modules.AudioPane
{
    public class WaveFormLoadingAdorner : Adorner
    {
        private AudioPaneView m_AudioPaneView;
        private Pen m_pen;
        private SolidColorBrush m_renderBrush;
        private Typeface m_typeFace;
        private CultureInfo m_culture;
        private Pen m_textPen;
        private Point m_pointText;
        private Rect m_rectRect;

        public WaveFormLoadingAdorner(FrameworkElement adornedElement, AudioPaneView view)
            : base(adornedElement)
        {
            IsHitTestVisible = false;
            ClipToBounds = true;
            m_AudioPaneView = view;

            m_renderBrush = new SolidColorBrush(Colors.Black) { Opacity = 0.6 };
            m_renderBrush.Freeze();

            m_pen = new Pen(Brushes.White, 1);
            m_pen.Freeze();

            m_typeFace = new Typeface("Helvetica");

            m_culture = CultureInfo.GetCultureInfo("en-us");
            
            m_textPen = new Pen(Brushes.Black, 1);
            m_textPen.Freeze();

            m_pointText = new Point(1, 1);
            m_rectRect = new Rect(1, 1, 1, 1);
        }

        public bool DisplayRecorderTime
        {
            get; set;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var formattedText = new FormattedText(
                (DisplayRecorderTime ? m_AudioPaneView.ViewModel.CurrentTimeString : UserInterfaceStrings.Loading),
                m_culture,
                FlowDirection.LeftToRight,
                m_typeFace,
                40,
                Brushes.Black
                );

            const double margin = 20;

            double width = ((FrameworkElement)AdornedElement).ActualWidth;
            double height = ((FrameworkElement)AdornedElement).ActualHeight - margin;

            if (width <= margin + margin || height <= margin + margin)
            {
                return;
            }

            double leftOffset = (width - formattedText.Width) / 2;
            double topOffset = (height - formattedText.Height) / 2;

            m_rectRect.X = margin;
            m_rectRect.Y = margin;
            m_rectRect.Width = width - margin - margin;
            m_rectRect.Height = height - margin - margin;

            drawingContext.DrawRoundedRectangle(m_renderBrush, m_pen,
                                                m_rectRect,
                                                10.0, 10.0);
            m_pointText.X = leftOffset;
            m_pointText.Y = topOffset;
            Geometry textGeometry = formattedText.BuildGeometry(m_pointText);

            drawingContext.DrawGeometry(Brushes.White,
                                        m_textPen,
                                        textGeometry);
        }
    }
}