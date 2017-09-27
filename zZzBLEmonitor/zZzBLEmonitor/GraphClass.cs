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

        private List<Polyline> plotsList = new List<Polyline>();

        private double tickX = 0, maxTickX = 0, axisX = 0, zeroY = 0, scale = 0;

        public void Background(Color backgndColor)
        {
            graphCanvas.Background = new SolidColorBrush(backgndColor);
        }

#region// Initializing
        public void Initialize(StackPanel graphContainer, double ticksX)
        {
            graphContainer.Children.Clear();
            graphCanvas.Children.Clear();
            graphContainer.Children.Add(graphCanvas);
            ResizeCanvas(graphContainer);
            tickX = ticksX;//Distance between X values in X axis
            axisX = 0;//Sets the X axis position to the next Tick
        }
#endregion
        public void AddPlot(Color plotColor)
        { 
            Polyline plot = new Polyline();//Creates new polyline
            plot.Stroke = new SolidColorBrush(plotColor);//Assigns the color
            Point pointZero = new Point(0, zeroY);//Point at the origin
            plot.Points.Add(pointZero);//Adds the zero point
            plotsList.Add(plot);//Adds plot to the list of plots
            graphCanvas.Children.Add(plotsList.Last());//Adds the last plot to canvas
        }
        public void Clear()//Clears all plots from the list
        {
            foreach(Polyline plot in plotsList)
            {
                plot.Points.Clear();
            }
        }

        public void AddPoints(double[] newPoints)
        {
            int k;
            if(plotsList.Count() > newPoints.Length)
            {
                k = newPoints.Length;
            }
            else
            {
                k = plotsList.Count();
            }
            for(int i = 0; i < k; i++)
            {
                plotsList.ElementAt(i).Points.Add(new Point(axisX, zeroY - newPoints[i] * scale));
            }

            graphCanvas.UpdateLayout();
            axisX += tickX;
            if(axisX > maxTickX)
            {
                axisX = 0;
                Clear();
            }
        }

        // Changes the size of the canvas to the size of the container
        public void ResizeCanvas(StackPanel graphContainer)
        {
            graphCanvas.Height = graphContainer.ActualHeight;
            graphCanvas.Width = graphContainer.ActualWidth;
            graphCanvas.UpdateLayout();
            zeroY = graphCanvas.ActualHeight / 2;//Setting Y zero at the center
            maxTickX = graphCanvas.ActualWidth;//Lenght of the X axis
            scale = zeroY / 180;
        }
    }
}
