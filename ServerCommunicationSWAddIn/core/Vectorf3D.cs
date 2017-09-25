using Newtonsoft.Json;

namespace ServerCommunicationSWAddIn.core
{
    /// <summary>
    /// Class that describes a vector3D of floats
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    public class Vectorf3D
    {
        /// <summary>
        /// x value of the vector
        /// </summary>
        private float x;

        /// <summary>
        /// y value of the vector
        /// </summary>
        private float y;

        /// <summary>
        /// z value of the vector
        /// </summary>
        private float z;

        /// <summary>
        /// Constructor of the class
        /// </summary>
        /// <param name="_x">The initial x value</param>
        /// <param name="_y">The initial y value</param>
        /// <param name="_z">The initial z value</param>
        public Vectorf3D(double _x, double _y, double _z)
        {
            x = (float)_x;
            y = (float)_y;
            z = (float)_z;
        }
    }
}
