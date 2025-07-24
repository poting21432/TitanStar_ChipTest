using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Support.Data
{
    public static class RandomExt
    {
        public static string RandomString(this Random random, int length, string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
        {
            char[] randomString = new char[length];

            for (int i = 0; i < length; i++)
                randomString[i] = characters[random.Next(characters.Length)];

            return new string(randomString);
        }
        public static double RandomDouble(this Random random, double lowerLimit, double upperLimit)
            => lowerLimit + random.NextDouble() * (upperLimit - lowerLimit);
    }
}
