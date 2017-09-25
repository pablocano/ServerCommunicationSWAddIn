namespace ServerCommunicationSWAddIn.util
{
    /// <summary>
    /// Static class used to encrypt and decrypt data
    /// </summary>
    class Cryptography
    {

        /// <summary>
        /// Encrypt an array of data, using a 128 bits key
        /// </summary>
        /// <param name="data">The data to encrypt</param>
        /// <param name="key">The key used to encrypt</param>
        public static void encrypt(ref uint[] data, uint[] key)
        {
            uint size = (uint)data.Length;
            uint y, z, sum;
            uint p, rounds, e;
            rounds = 6 + 52 / size;
            sum = 0;
            z = data[size - 1];
            do
            {
                sum += 0x9e3779b9;
                e = (sum >> 2) & 3;
                for (p = 0; p < size - 1; p++)
                {
                    y = data[p + 1];
                    z = data[p] += (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (key[(p & 3) ^ e] ^ z)));
                }
                y = data[0];
                z = data[size - 1] += (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (key[(p & 3) ^ e] ^ z)));
            }
            while (0 < --rounds);
        }

        /// <summary>
        /// Decrypt the data that was encrypted using the encrypt function of this class, and with the same key
        /// </summary>
        /// <param name="data">The data to decrypt</param>
        /// <param name="key">The key used to encrypt the original data</param>
        public static void decrypt(ref uint[] data, uint[] key)
        {
            uint size = (uint)data.Length;
            uint y, z, sum;
            uint p, rounds, e;
            rounds = 6 + 52 / size;
            sum = rounds * 0x9e3779b9;
            y = data[0];
            do
            {
                e = (sum >> 2) & 3;
                for (p = size - 1; p > 0; p--)
                {
                    z = data[p - 1];
                    y = data[p] -= (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (key[(p & 3) ^ e] ^ z)));
                }
                z = data[size - 1];
                y = data[0] -= (((z >> 5 ^ y << 2) + (y >> 3 ^ z << 4)) ^ ((sum ^ y) + (key[(p & 3) ^ e] ^ z)));
                sum -= 0x9e3779b9;
            } while (0 < --rounds);
        }
    }
}
