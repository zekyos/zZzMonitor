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

    // <<<>>> Graph Class <<<>>>
    //_______________________________________________________________________
    // Requires a container of type StackPanel to add a canvas element to it
    // as a children.
    // Plots lines using a Shape of Polyline class, where each point in the
    // polyline is a data point.
    //_______________________________________________________________________
    //
    // >>> Properties
    //
    // ** AxisX
    //    Is the width of the X axis defined from the Actual width of the Canvas
    // ** TickX
    //    Is the X tick distance in pixels
    // ** ZeroY
    //    Is the position on the Canvas that corresponds to zero in Y. This value
    //    is set to the Actual Height of the canvas at the inicialization.
    // ** ScrollX
    //    Enables the scrolling of the data in the X axis. When the data points
    //    reach the end of AxisX, the first point is eliminated and the polyline
    //    dataPlot starts to move.
    //
    // >>> Methods
    //
    // ** Initialize( StackPanel graphContainer, double ticks, bool scroll)
    //      This should be called before performing any operations with the graph.
    //      Clears the children in the graphContainer and graphCanvas, and also
    //      deletes the points in the dataPlot. Sets the TickX and ScrollX values
    //      (if present). Then adds the graphCanvas to the graphContainer as a 
    //      child, Resizes the graphCanvas to the size of the graphContainer, adds
    //      a point at the origin (x=0, y= ZeroY) and updates the layout.
    //
    //____________________________________________________________________________

    public class GraphClass
    {
        private Canvas graphCanvas = new Canvas();

        private Polyline xAccPlot = new Polyline();
        private Polyline yAccPlot = new Polyline();
        private Polyline zAccPlot = new Polyline();
        private Polyline xGyroPlot = new Polyline();
        private Polyline yGyroPlot = new Polyline();
        private Polyline zGyroPlot = new Polyline();

        private double tickX = 0, maxTickX = 0, axisX = 0, zeroY = 0, scaleAcc = 0, scaleGyro = 0;

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

            xAccPlot.Stroke = new SolidColorBrush(Colors.Black);//AccX Plot Color
            yAccPlot.Stroke = new SolidColorBrush(Colors.DarkBlue);//AccY Plot Color
            zAccPlot.Stroke = new SolidColorBrush(Colors.DarkGreen);//AccZ Plot Color
            xGyroPlot.Stroke = new SolidColorBrush(Colors.Gray);//Gyro X Plot Color
            yGyroPlot.Stroke = new SolidColorBrush(Colors.DodgerBlue);//Gyro Y Plot Color
            zGyroPlot.Stroke = new SolidColorBrush(Colors.LimeGreen);//Gyro Z Plot Color
            Point pointZero = new Point(0, zeroY);//Point at the origin

            xAccPlot.Points.Add(pointZero);
            yAccPlot.Points.Add(pointZero);
            zAccPlot.Points.Add(pointZero);
            xGyroPlot.Points.Add(pointZero);
            yGyroPlot.Points.Add(pointZero);
            zGyroPlot.Points.Add(pointZero);

            //graphCanvas.Children.Add(xAccPlot);
            //graphCanvas.Children.Add(yAccPlot);
            //graphCanvas.Children.Add(zAccPlot);
            graphCanvas.Children.Add(xGyroPlot);
            graphCanvas.Children.Add(yGyroPlot);
            graphCanvas.Children.Add(zGyroPlot);

            Clear();
        }
#endregion
        public void Clear()//Clears all points from the plots
        {
            xAccPlot.Points.Clear();
            yAccPlot.Points.Clear();
            zAccPlot.Points.Clear();
            xGyroPlot.Points.Clear();
            yGyroPlot.Points.Clear();
            zGyroPlot.Points.Clear();
        }

        public void AddPoints(IMUdata newPoints)
        {
            xAccPlot.Points.Add(new Point(axisX, zeroY - newPoints.DataIMU[0] * scaleAcc));//Acc X point
            yAccPlot.Points.Add(new Point(axisX, zeroY - newPoints.DataIMU[1] * scaleAcc));//Acc Y point
            zAccPlot.Points.Add(new Point(axisX, zeroY - newPoints.DataIMU[2] * scaleAcc));//Acc Z point
            xGyroPlot.Points.Add(new Point(axisX, zeroY - newPoints.DataIMU[3] * scaleGyro));//Gyro X point
            yGyroPlot.Points.Add(new Point(axisX, zeroY - newPoints.DataIMU[4] * scaleGyro));//Gyro Y point
            zGyroPlot.Points.Add(new Point(axisX, zeroY - newPoints.DataIMU[5] * scaleGyro));//Gyro Z point

            graphCanvas.UpdateLayout();
            axisX += tickX;
            if(axisX > maxTickX)
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
            scaleAcc = zeroY / 2000;
            scaleGyro = zeroY / 250;
        }
    }
}
