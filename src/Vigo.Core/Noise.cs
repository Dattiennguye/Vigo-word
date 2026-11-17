using System;

namespace Vigo.Core
{
    /// <summary>Gradient noise 2D (Perlin cải tiến) có seed.</summary>
    public sealed class Noise2D
    {
        private readonly byte[] _p = new byte[512];

        public Noise2D(ulong seed)
        {
            var r = new Rng(seed);
            var a = new byte[256];
            for (int i = 0; i < 256; i++) a[i] = (byte)i;
            for (int i = 255; i > 0; i--) { int j = r.Range(0, i + 1); byte t = a[i]; a[i] = a[j]; a[j] = t; }
            for (int i = 0; i < 512; i++) _p[i] = a[i & 255];
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Grad(int h, float x, float y)
        {
            switch (h & 7)
            {
                case 0: return x + y; case 1: return -x + y; case 2: return x - y; case 3: return -x - y;
                case 4: return x; case 5: return -x; case 6: return y; default: return -y;
            }
        }

        /// <summary>Xấp xỉ [-1, 1].</summary>
        public float Value(float x, float y)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float xf = x - xi, yf = y - yi;
            xi &= 255; yi &= 255;
            float u = Fade(xf), v = Fade(yf);
            int aa = _p[_p[xi] + yi], ab = _p[_p[xi] + yi + 1], ba = _p[_p[xi + 1] + yi], bb = _p[_p[xi + 1] + yi + 1];
            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
            return Lerp(x1, x2, v) * 1.25f;
        }

        public float Fbm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0, amp = 1, norm = 0, f = 1;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Value(x * f, y * f); norm += amp; amp *= gain; f *= lacunarity;
            }
            float r = sum / norm;
            return r < -1f ? -1f : (r > 1f ? 1f : r);
        }

        /// <summary>Ridged multifractal, [0, 1]. Đỉnh sắc dọc đường noise = 0 → tạo dãy núi.</summary>
        public float Ridged(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0, amp = 1, norm = 0, f = 1;
            for (int o = 0; o < octaves; o++)
            {
                float n = 1f - Math.Abs(Value(x * f, y * f));
                sum += amp * n * n; norm += amp; amp *= gain; f *= lacunarity;
            }
            float r = sum / norm;
            return r < 0f ? 0f : (r > 1f ? 1f : r);
        }
    }

    internal sealed class MinHeap
    {
        private int[] _items = new int[64];
        private float[] _pri = new float[64];
        private int _count;
        public int Count => _count;
        public void Clear() { _count = 0; }

        public void Push(int item, float p)
        {
            if (_count == _items.Length) { Array.Resize(ref _items, _count * 2); Array.Resize(ref _pri, _count * 2); }
            int i = _count++;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_pri[parent] <= p) break;
                _items[i] = _items[parent]; _pri[i] = _pri[parent]; i = parent;
            }
            _items[i] = item; _pri[i] = p;
        }

        public int Pop(out float p)
        {
            int top = _items[0]; p = _pri[0];
            _count--;
            if (_count > 0)
            {
                int item = _items[_count]; float pr = _pri[_count];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1;
                    if (l >= _count) break;
                    int r = l + 1;
                    int c = (r < _count && _pri[r] < _pri[l]) ? r : l;
                    if (_pri[c] >= pr) break;
                    _items[i] = _items[c]; _pri[i] = _pri[c]; i = c;
                }
                _items[i] = item; _pri[i] = pr;
            }
            return top;
        }
    }
}
