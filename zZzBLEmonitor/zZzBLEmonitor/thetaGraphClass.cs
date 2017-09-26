using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Navigation;
using System.Diagnostics;
using Windows.Storage.Streams;
using Windows.Security.Cryptography;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;

namespace zZzBLEmonitor
{
    class thetaGraphClass
    {
        private Canvas graphCanvas = new Canvas();

        private Polyline xPlot = new Polyline();
        private Polyline yPlot = new Polyline();
        private Polyline zPlot = new Polyline();
        private Polyline position = new Polyline();

        private double tickX = 0, maxTickX = 0, axisX = 0, zeroY = 0, scale = 0;

        public void Background()
        {
            graphCanvas.Background = new SolidColorBrush(Colors.WhiteSmoke);
        }

        #region// Initializing
        public void Initialize(StackPanel graphContainer, double ticksX)
        {
            graphContainer.Children.Clear();
            graphCanvas.Children.Clear();
            AttachCanvas(graphContainer);
            ResizeCanvas(graphContainer);
            tickX = ticksX;//Distance between X values in X axis
            axisX = 0;//Sets the X axis position to the next Tick

            xPlot.Stroke = new SolidColorBrush(Colors.Black);//AccX Plot Color
            yPlot.Stroke = new SolidColorBrush(Colors.DarkBlue);//AccY Plot Color
            zPlot.Stroke = new SolidColorBrush(Colors.DarkGreen);//AccZ Plot Color
            position.Stroke = new SolidColorBrush(Colors.Coral);//AccZ Plot Color
            Point pointZero = new Point(0, zeroY);//Point at the origin

            xPlot.Points.Add(pointZero);
            yPlot.Points.Add(pointZero);
            zPlot.Points.Add(pointZero);
            position.Points.Add(pointZero);

            graphCanvas.Children.Add(xPlot);
            graphCanvas.Children.Add(yPlot);
            graphCanvas.Children.Add(zPlot);
            graphCanvas.Children.Add(position);

            Clear();
        }
        #endregion
        public void Clear()//Clears all points from the plots
        {
            xPlot.Points.Clear();
            yPlot.Points.Clear();
            zPlot.Points.Clear();
            position.Points.Clear();
        }

        public void AddPoints(thetaClass newPoints)
        {
            xPlot.Points.Add(new Point(axisX, zeroY - newPoints.ThetaX * scale));//Acc X point
            yPlot.Points.Add(new Point(axisX, zeroY - newPoints.ThetaY * scale));//Acc Y point
            zPlot.Points.Add(new Point(axisX, zeroY - newPoints.ThetaZ * scale));//Acc Z point
            position.Points.Add(new Point(axisX, zeroY - newPoints.position * 100));//Acc Z point

            graphCanvas.UpdateLayout();
            axisX += tickX;
            if (axisX > maxTickX)
            {
                axisX = 0;
                Clear();
            }
        }

        // Adds the graphCanvas as a children to a StackPanel
        public void AttachCanvas(StackPanel graphContainer)
        {
            graphContainer.Children.Add(graphCanvas);
        }

        // Changes the size of the canvas to the size of the container
        public void ResizeCanvas(StackPanel graphContainer)
        {
            graphCanvas.Height = graphContainer.ActualHeight;
            graphCanvas.Width = graphContainer.ActualWidth;
            graphCanvas.UpdateLayout();
            zeroY = graphCanvas.ActualHeight / 2;//Setting Y zero at the center
            maxTickX = graphCanvas.ActualWidth;//Lenght of the X axis
            scale = zeroY / 180;//Sets the upper and lower bounds to +-180deg
        }
    }
}
