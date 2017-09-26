using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zZzBLEmonitor
{
    public class IMUdata
    {
        //private double xAcc, yAcc, zAcc, xGyr, yGyr, zGyr = 0;
        private double[] data = new double[6];
        private DateTime timeStamp = DateTime.Now;
        public double[] DataIMU
        {
            get
            {
                return data;
            }
            set
            {
                data = value;          
                /*data[0] = xAcc = value[0];
                data[1] = yAcc = value[1];
                data[2] = zAcc = value[2];
                data[3] = xGyr = value[3];
                data[4] = yGyr = value[4];
                data[5] = zGyr = value[5];*/

            }
        }

        public string StringIMU
        {
            get
            {
                StringBuilder stringData = new StringBuilder();
                foreach (double element in data)
                {
                    stringData.Append(element.ToString()+',');
                }
                stringData.AppendLine(timeStamp.ToString("HH,mm,ss,fff"));
                return stringData.ToString(); ;
            }
        }
        
        //Accelerometer values
        public double xAccValue
        { get { return data[0]; } }
        public double yAccValue
        { get { return data[1]; } }
        public double zAccValue
        { get { return data[2]; } }
        //Gyroscope values
        public double xGyrValue
        { get { return data[3]; } }
        public double yGyrValue
        { get { return data[4]; } }
        public double zGyrValue
        { get { return data[5]; } }

        public DateTime TimeStamp
        {
            get { return timeStamp; }
            set { timeStamp = value; }
        }
    }
}
