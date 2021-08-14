using System;

namespace InventoryInventor.Preset
{
    public class IdGenerator
    {
        private static readonly Random _random = new Random();
        private const string Pool = "ABCDEFGHIJKLMNPQRSTUVWXYZabcdefghjklmnopqrstuvwxyz0123456789";

        public static string Generate()
        {
            var output = new char[6];

            for (var i = 0; i < output.Length; i++)
            {
                output[i] = Pool[_random.Next(0, Pool.Length)];
            }

            return new string(output);
        }
    }
}