using System;

namespace Vigo.Core
{
    /// <summary>xoshiro256** khởi tạo bằng splitmix64. Tất định, không phụ thuộc System.Random.</summary>
    public sealed class Rng
    {
        private ulong _s0, _s1, _s2, _s3;

        public Rng(ulong seed)
        {
            ulong x = seed;
            _s0 = SplitMix(ref x); _s1 = SplitMix(ref x); _s2 = SplitMix(ref x); _s3 = SplitMix(ref x);
        }

        private static ulong SplitMix(ref ulong x)
        {
            ulong z = (x += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

        public ulong NextU64()
        {
            ulong result = Rotl(_s1 * 5, 7) * 9;
            ulong t = _s1 << 17;
            _s2 ^= _s0; _s3 ^= _s1; _s1 ^= _s2; _s0 ^= _s3;
            _s2 ^= t; _s3 = Rotl(_s3, 45);
            return result;
        }

        public double NextDouble() => (NextU64() >> 11) * (1.0 / 9007199254740992.0);
        public float NextFloat() => (float)NextDouble();

        /// <summary>[min, max)</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextU64() % (ulong)(max - min));
        }

        public float Range(float min, float max) => min + (max - min) * NextFloat();
        public bool Chance(double p) => NextDouble() < p;

        public double Gaussian()
        {
            double u1 = 1.0 - NextDouble(), u2 = NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        public static ulong HashString(string s)
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 1099511628211UL; }
            return h;
        }

        /// <summary>Luồng RNG riêng theo tên bước: đổi một bước không làm đổi các bước khác.</summary>
        public static Rng Stream(ulong worldSeed, string name, int salt = 0)
        {
            ulong x = worldSeed ^ (HashString(name) * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)salt * 0xD6E8FEB86659FD93UL);
            return new Rng(x);
        }
    }
}
