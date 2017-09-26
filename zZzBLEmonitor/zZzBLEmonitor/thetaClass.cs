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
        private DateTime timeStamp = new DateTime();

        // Adds the angles to the class and a timestamp
        public double[] ThetaData
        {
            get{ return theta; }
            set
            {
                theta = value;
                timeStamp = DateTime.Now;
            }
        }
        public byte position
        {
            get { return position; }
            set { position = value; }
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
                stringData.Append(position.ToString() + ',');
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
    }
}
