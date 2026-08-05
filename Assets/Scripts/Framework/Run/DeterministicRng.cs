using System;

namespace FFSS.Framework.Run
{
    [Serializable]
    public sealed class DeterministicRng
    {
        private const uint FallbackSeed = 0x6D2B79F5u;

        public uint state;

        public DeterministicRng(int seed)
        {
            state = seed == 0 ? FallbackSeed : unchecked((uint)seed);
        }

        public uint NextUInt()
        {
            uint value = state == 0 ? FallbackSeed : state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }

        public int Range(int minimumInclusive, int maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumExclusive));
            }

            uint range = unchecked((uint)(maximumExclusive - minimumInclusive));
            return minimumInclusive + (int)(NextUInt() % range);
        }

        public float Value()
        {
            return (NextUInt() & 0x00FFFFFFu) / 16777216f;
        }
    }
}
