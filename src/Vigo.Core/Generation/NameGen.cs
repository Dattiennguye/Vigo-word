using System;
using System.Collections.Generic;
using System.Text;

namespace Vigo.Core
{
    /// <summary>Tên quốc gia/tỉnh/thành phố theo "văn hóa" (bộ âm tiết riêng) + quốc kỳ hình học từ seed.</summary>
    internal sealed class NameGen
    {
        private sealed class Culture
        {
            public string[] Onset, Vowel, Coda, CountrySuffix;
        }

        private static readonly Culture[] Cultures =
        {
            new Culture { Onset = new[]{"ka","ra","mo","ta","vel","dor","an","sa","bre","tur"}, Vowel = new[]{"a","e","o","i","u"}, Coda = new[]{"n","r","s","th","k","l","d"}, CountrySuffix = new[]{"ia","land","mark","ora"} },
            new Culture { Onset = new[]{"zhe","lin","han","wei","yu","tao","mei","qi","shan","fen"}, Vowel = new[]{"a","i","o","e","u"}, Coda = new[]{"n","ng","","i","o"}, CountrySuffix = new[]{"guo","yan","dao","an"} },
            new Culture { Onset = new[]{"al","ib","sah","mar","kha","zan","tar","ham","nur","say"}, Vowel = new[]{"a","i","u","e"}, Coda = new[]{"d","m","r","ib","an","iz"}, CountrySuffix = new[]{"istan","ar","aya","ud"} },
            new Culture { Onset = new[]{"ve","lu","fi","gra","pi","cor","mer","sol","bel","nor"}, Vowel = new[]{"a","e","i","o"}, Coda = new[]{"n","ro","ma","to","re","s"}, CountrySuffix = new[]{"ana","onia","ia","esca"} },
            new Culture { Onset = new[]{"ob","kre","ste","vla","dro","zim","ne","pol","ro","sli"}, Vowel = new[]{"a","o","u","y","e"}, Coda = new[]{"v","k","sk","gorod","grad","ev"}, CountrySuffix = new[]{"ovia","stan","ska","ria"} },
            new Culture { Onset = new[]{"ngu","tha","pho","kim","hoa","lan","bin","duo","xuan","ban"}, Vowel = new[]{"a","o","e","i"}, Coda = new[]{"n","ng","m","","c"}, CountrySuffix = new[]{"nam","viet","la","phan"} },
        };

        public static int CultureCount => Cultures.Length;

        private readonly Rng _rng;
        private readonly HashSet<string> _used = new HashSet<string>();
        public NameGen(Rng rng) { _rng = rng; }

        private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private string Syllables(Culture c, int count)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                sb.Append(c.Onset[_rng.Range(0, c.Onset.Length)]);
                if (_rng.Chance(0.6)) sb.Append(c.Vowel[_rng.Range(0, c.Vowel.Length)]);
                if (i == count - 1 && _rng.Chance(0.7)) sb.Append(c.Coda[_rng.Range(0, c.Coda.Length)]);
            }
            return sb.ToString();
        }

        private string Unique(string name)
        {
            string n = name; int k = 2;
            while (!_used.Add(n)) n = name + " " + ToRoman(k++);
            return n;
        }

        private static string ToRoman(int v) { string[] r = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" }; return v < r.Length ? r[v] : v.ToString(); }

        public string Country(int culture)
        {
            var c = Cultures[culture % Cultures.Length];
            string stem = Cap(Syllables(c, _rng.Range(1, 3)));
            string suf = c.CountrySuffix[_rng.Range(0, c.CountrySuffix.Length)];
            if (stem.Length > 0 && suf.Length > 0 && "aeiou".IndexOf(char.ToLowerInvariant(stem[stem.Length - 1])) >= 0 && "aeiou".IndexOf(suf[0]) >= 0) stem = stem.Substring(0, stem.Length - 1);
            return Unique(stem + suf);
        }

        public string Place(int culture)
        {
            var c = Cultures[culture % Cultures.Length];
            return Unique(Cap(Syllables(c, _rng.Range(2, 4))));
        }

        public FlagSpec Flag(int index)
        {
            var spec = new FlagSpec();
            spec.Pattern = (FlagPattern)_rng.Range(0, 8);
            double hue = (index * 0.61803398875 + _rng.NextDouble() * 0.08) % 1.0;
            spec.Primary = Hsv(hue, 0.70, 0.78);
            spec.Secondary = _rng.Chance(0.5) ? 0xF2F2F2 : Hsv((hue + 0.5) % 1.0, 0.6, 0.85);
            spec.Accent = _rng.Chance(0.5) ? 0xF5C518 : 0x1B1B1B;
            return spec;
        }

        public static int Hsv(double h, double s, double v)
        {
            double r = 0, g = 0, b = 0;
            int i = (int)(h * 6); double f = h * 6 - i, p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
            switch (i % 6)
            {
                case 0: r = v; g = t; b = p; break; case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break; case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break; default: r = v; g = p; b = q; break;
            }
            return ((int)(r * 255) << 16) | ((int)(g * 255) << 8) | (int)(b * 255);
        }
    }
}

namespace Vigo.Core
{
    public static class NameGenAccess
    {
        public static int Hsv(double h, double s, double v) => NameGen.Hsv(h, s, v);
    }
}
