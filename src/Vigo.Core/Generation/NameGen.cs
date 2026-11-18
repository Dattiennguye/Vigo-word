using System;
using System.Collections.Generic;
using System.Text;

namespace Vigo.Core
{
    /// <summary>Tên quốc gia/tỉnh/thành phố theo "văn hóa" (bộ âm tiết riêng) + quốc kỳ hình học từ seed.</summary>
    internal sealed class NameGen
    {
        // Mỗi "văn hóa" là một vùng đặt tên thuần Việt: tiền tố + tên riêng (ghép thành địa danh 2–3 chữ).
        private sealed class Culture { public string[] First, Second, Third, CountryHead, CountryTail; }

        private static string[] W(string s) => s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        private static string[] WW(string s) => s.Split('|');

        private static readonly Culture[] Cultures =
        {
            // 0 — Hán-Việt, vùng châu thổ sông Hồng
            new Culture { First = WW("Thăng|Hoài|Từ|Gia|Đông|Tây|Nam|Bắc|Văn|Thái|Hưng|Phú|Vĩnh|An|Bảo|Cổ|Kim|Long|Hồng|Ninh|Quang|Thanh|Tiên|Thiên|Yên|Hà|Mỹ|Đại|Lạc|Sơn"),
                Second = WW("Long|Đức|Lâm|Bình|Hà|Giang|Châu|Lộc|Thọ|Phong|Phúc|Xuyên|Hòa|Định|Khê|Thành|Tân|Trì|An|Đông|Sơn|Liên|Lễ|Nghĩa|Chương|Mai|Lâu|Cát|Vũ|Hải"), Third = WW("Thượng|Hạ|Trung|Đông|Tây|Nam|Bắc|Cổ|Tân"),
                CountryHead = WW("Đại|Đại|Vạn|Thiên|Văn|Hồng|Đông|Bách"), CountryTail = WW("Việt|Lạc|Nam|Hưng|Xuân|Lang|Hùng|Thái|Phúc|Bình|Đức|Long|Minh|Ninh") },
            // 1 — Thuần Việt, làng quê Bắc Bộ
            new Culture { First = WW("Đồng|Bến|Cầu|Chợ|Gò|Đầm|Rừng|Hóc|Bãi|Làng|Xóm|Phố|Đình|Chùa|Cống|Đê|Ngã|Eo|Cửa|Ao|Nương|Vườn|Giếng|Cồn|Đò|Bờ|Mả|Trại|Nghè|Hang"),
                Second = WW("Sen|Mơ|Lau|Sậy|Cau|Tre|Trúc|Đa|Gạo|Nhãn|Vải|Cam|Dâu|Cói|Muỗm|Si|Bần|Mía|Dừa|Hoa|Rồng|Hạc|Cò|Chim|Cá|Nước|Đá|Cát|Vàng|Bạc"), Third = WW("Cả|Con|Lớn|Nhỏ|Trong|Ngoài|Mới|Cũ"),
                CountryHead = WW("Xứ|Miền|Cõi|Đất|Vùng"), CountryTail = WW("Lúa|Sen|Lạc|Việt|Mây|Nước|Hạc|Rồng|Cau|Trúc|Đa|Cò") },
            // 2 — vùng núi phía Bắc (Tày, Thái, Mường...)
            new Culture { First = WW("Mường|Bản|Pác|Lũng|Nà|Phìn|Sín|Tà|Khau|Tả|Hoàng|Suối|Thác|Đèo|Đỉnh|Sa|Cao|Lèng|Pù|Còn|Mù|Khe|Nậm|Mộc|Điện|Quản|Bắc|Than|Hồng|Lai"),
                Second = WW("Lay|Chải|Phìn|Lả|Sang|Cang|Pha|Lằn|Xá|Ngần|Bó|Liềng|Phố|Mạ|Hoa|Pao|Lù|Cò|Mè|Nam|Sơn|Mai|Châu|Hạ|Lào|Cai|Quỳ|Hiền|Loong|Bung"), Third = WW("Thượng|Hạ|Trong|Ngoài|Nội|Ngoại"),
                CountryHead = WW("Mường|Xứ|Đất|Đại"), CountryTail = WW("Thái|Tày|Lô|Hạ|Lạc|Mường|Thượng|Bản|Sơn|Cao|Mèo|Lô|Ngàn") },
            // 3 — miền Trung
            new Culture { First = WW("Phú|Quảng|Thừa|Bình|Phước|Hội|Tam|Thạch|Duy|Điện|Đại|Tuy|Sông|Hòa|Phong|Thanh|Nghĩa|Hương|Triệu|Lệ|Vĩnh|Cam|Ninh|Diên|Khánh|Mỹ|An|Quy|Lộc|Trà"),
                Second = WW("Lộc|Điền|Đông|Tây|Phong|Nam|Bắc|An|Hiệp|Kỳ|Xuyên|Thủy|Quang|Hưng|Lễ|Mỹ|Hải|Châu|Ngãi|Sơn|Hòa|Thọ|Phước|Hà|Giang|Linh|Nha|Ranh|Dương|Vân"), Third = WW("Thượng|Hạ|Trung|Tây|Đông|Nam|Bắc"),
                CountryHead = WW("Đại|Phú|Quảng|Thuận|Chiêm|Hoan"), CountryTail = WW("Hóa|Nam|Ngãi|Bình|Trị|Phước|Lộc|Châu|Thuận|Ngãi|Hòa|Yên|Khánh|Đức|Phong") },
            // 4 — Nam Bộ, đồng bằng sông Cửu Long
            new Culture { First = WW("Cần|Mỹ|Tân|Long|Vĩnh|Thới|Tiền|Hậu|Bến|Cái|Rạch|Kinh|Sa|Mộc|Châu|Gò|Cà|Bà|Vàm|Ô|Trà|Sóc|Hòa|Phú|Tam|Thạnh|Đồng|Hưng|An|Bình"),
                Second = WW("Thơ|Tho|Tre|Giá|Nhum|Bè|Vinh|Lai|Xoài|Sắn|Môn|Cạn|Vang|Hóa|Phụng|Tràm|Bắp|Đước|Mắm|Chanh|Sen|Cói|Lộc|Hưng|Phước|Lợi|Tây|Đông|Thạnh|Quới"), Third = WW("A|B|Đông|Tây|Nam|Bắc|Trung"),
                CountryHead = WW("Vạn|Cửu|Hậu|Tiền|Đồng|Long|Tây"), CountryTail = WW("Long|Hóa|Lợi|Tho|Phước|Giang|Quới|Thạnh|Hưng|Ninh|Châu|Bình|Hưng|Tiên") },
            // 5 — duyên hải & hải đảo
            new Culture { First = WW("Cửa|Hải|Vũng|Mũi|Đảo|Vịnh|Bãi|Hòn|Lạch|Cảng|Biển|Sóng|Gành|Cồn|Phước|Long|Thuận|Vân|Cát|Hà|Nha|Quy|Phú|Sa|Cô|Lý|Cam|Vạn|Tuy|Ninh"),
                Second = WW("Lò|Hội|Rạng|Nai|Chài|Dừa|Ngư|Bàng|Nồm|Gió|Tiên|Rùa|Mực|Nghêu|Sò|Cá|Hồng|Ngọc|Mây|Sơn|Vân|Đài|Phong|Hưng|Tùng|Cò|Đôi|Khơi|Trăng|Hạ"), Third = WW("Lớn|Nhỏ|Đông|Tây|Nam|Bắc|Ngoài|Trong"),
                CountryHead = WW("Hải|Đại|Hoàng|Thiên|Vạn"), CountryTail = WW("Châu|Hải|Dương|Đảo|Vân|Hà|Nguyên|Lạc|Phong|Ngư|Hưng|Hồng|Đài|Sa|Lam") },
        };

        public static int CultureCount => Cultures.Length;

        private readonly Rng _rng;
        private readonly HashSet<string> _used = new HashSet<string>();
        public NameGen(Rng rng) { _rng = rng; }

        private static readonly string[] Extra = { "Thượng", "Hạ", "Trung", "Đông", "Tây", "Nam", "Bắc", "Tân", "Cổ", "Nội", "Ngoại" };

        private string Unique(string name)
        {
            string n = name; int k = 0;
            while (!_used.Add(n)) { k++; n = k <= Extra.Length ? name + " " + Extra[(k - 1 + (name.Length % Extra.Length)) % Extra.Length] : name + " " + (k - Extra.Length + 1); }
            return n;
        }

        private string Pick(string[] a) => a[_rng.Range(0, a.Length)];

        public string Country(int culture)
        {
            var c = Cultures[culture % Cultures.Length];
            string head = Pick(c.CountryHead), tail = Pick(c.CountryTail);
            if (head == tail) tail = Pick(c.CountryTail);
            string n = head + " " + tail;
            return Unique(n);
        }

        public string Place(int culture)
        {
            var c = Cultures[culture % Cultures.Length];
            string n = Pick(c.First) + " " + Pick(c.Second);
            if (_rng.Chance(0.12)) n += " " + Pick(c.Third);
            return Unique(n);
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
