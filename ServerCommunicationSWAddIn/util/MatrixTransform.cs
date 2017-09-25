using ServerCommunicationSWAddIn.core;
using SolidWorks.Interop.sldworks;
using System;

namespace ServerCommunicationSWAddIn.util
{
    public class MatrixTransform
    {

        private double[] data;

        public MatrixTransform(MathTransform transform)
        {
            data = (double[])transform.ArrayData;
        }


        public Vectorf3D EulerAngles()
        {
            double xRot;
            double yRot;
            double zRot; 

            if (data[6] != 1 && data[6] != -1)
            {
                xRot = Math.Atan2(data[7], data[8]);
                yRot = -Math.Asin(data[6]);
                zRot = Math.Atan2(data[3], data[0]);
            }
            else
            {
                zRot = 0;
                if(data[6] != -1)
                {
                    yRot = Math.PI/2.0;
                    xRot = Math.Atan2(data[1], data[2]);
                }
                else
                {
                    yRot = -Math.PI / 2.0;
                    xRot = Math.Atan2(-data[1], -data[2]);
                }
            }

            return new Vectorf3D(xRot, yRot, zRot);
        }

        public Vectorf3D Translation()
        {
            return new Vectorf3D(data[9], data[10], data[11]);
        }

    }
}
