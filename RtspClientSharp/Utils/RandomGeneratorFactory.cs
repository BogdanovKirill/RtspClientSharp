using System;

namespace RtspClientSharp.Utils
{
    static class RandomGeneratorFactory
    {
        private static readonly Random SeedGenerator = new Random(Guid.NewGuid().GetHashCode());

        public static Random CreateGenerator()
        {
            int seed;

            lock (SeedGenerator)
                seed = SeedGenerator.Next();

            return new Random(seed);
        }
    }
}