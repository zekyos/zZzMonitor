using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zZzBLEmonitor
{
    public class thetaClass
    {
        private double[] theta = new double[3];
        private int position = 0;
        private DateTime timeStamp = new DateTime();

        // Adds the angles to the class and a timestamp
        public double[] ThetaData
        {
            get
            {
                double[] thetaData = new double[4];
                for (int i = 0; i < 3; i++)
                    thetaData[i] = theta[i];
                thetaData[3] = (double)position * 10;
                return thetaData;
            }
            set
            {
                theta = value;
                timeStamp = DateTime.Now;
            }
        }
        // String builder for the angles
        public string StringTheta
        {
            get
            {
                StringBuilder stringData = new StringBuilder();
                foreach (double element in theta)
                {
                    stringData.Append(element.ToString() + ',');
                }
                stringData.AppendLine(timeStamp.ToString("HH,mm,ss,fff"));
                return stringData.ToString(); ;
            }
        }

        // Access to the individual angles
        public double ThetaZ
        { get { return theta[0]; } }
        public double ThetaY
        { get { return theta[1]; } }
        public double ThetaX
        { get { return theta[2]; } }
        public int Position
        { get { return position; }
          set { position = value; } }
    }
}
