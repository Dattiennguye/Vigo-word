using System;
using System.Collections.Generic;

namespace Vigo.Core
{
    /// <summary>Cấu hình sinh thế giới — mọi tham số chỉnh được (GDD II.1).</summary>
    public sealed class WorldConfig
    {
        public ulong Seed = 12345;
        public float MapWidth = 2000f;
        public float MapHeight = 1400f;
        /// <summary>Số ô mục tiêu của đồ thị ô (thực tế lệch vài %).</summary>
        public int CellCount = 40000;
        public int CountryCount = 30;
        public int MinProvincesPerCountry = 8;
        public int MaxProvincesPerCountry = 20;
        public int MinCitiesPerProvince = 2;
        public int MaxCitiesPerProvince = 8;
        public float LandRatio = 0.45f;
        public float MountainFactor = 1f;
        public float ResourceRichness = 1f;
        public int MinCellsPerProvince = 6;
        public int MaxAttempts = 4;
    }

    public sealed class ValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public bool IsValid => Errors.Count == 0;
        public void Fail(string msg) { Errors.Add(msg); }
    }

    /// <summary>
    /// Nguồn sự thật duy nhất của thế giới. Dữ liệu ô theo dạng mảng (struct-of-arrays);
    /// Country/Province/City là danh sách nhỏ nên dùng lớp đơn giản ở Giai đoạn 1.
    /// </summary>
    public sealed class WorldState
    {
        public WorldConfig Config;
        public ulong Seed;
        public int Attempt;
        public CellGrid Grid;
        public int CellCount => Grid.Count;

        // ---- Địa lý theo ô ----
        public float[] Elevation;      // >0 đất, <=0 nước; đất chuẩn hóa ~[0,1]
        public float[] Temperature;    // 0..1
        public float[] Moisture;       // 0..1
        public float[] Fertility;      // 0..1
        public float[] Slope;          // 0..1
        public float[] Flux;           // lưu lượng nước tích lũy
        public float[] FilledElevation;
        public byte[] Biomes;
        public bool[] IsLand;          // elevation > 0 (gồm cả hồ nội địa nhỏ)
        public bool[] IsOcean;         // nước nối với rìa bản đồ
        public bool[] IsLake;          // hồ / biển kín
        public bool[] IsRiver;
        public int[] FlowTo;           // ô hạ lưu, -1 nếu là biển
        public float RiverThreshold;
        /// <summary>Ngưỡng độ cao theo phân vị đất (80% / 94%).</summary>
        public float HighlandElev, MountainElev;

        // ---- Chính trị theo ô ----
        public int[] CountryOfCell;    // -1 chưa có chủ
        public int[] ProvinceOfCell;
        public int[] CityOfCell;
        public bool[] IsRoad;
        public bool[] IsBridge;

        public List<Country> Countries = new List<Country>();
        public List<Province> Provinces = new List<Province>();
        public List<City> Cities = new List<City>();
        public List<ResourceDeposit> Deposits = new List<ResourceDeposit>();
        public List<RoadSegment> Roads = new List<RoadSegment>();

        public ValidationReport Validation;
        public List<string> GenerationNotes = new List<string>();

        public Country GetCountry(CountryId id) => Countries[id.Value];
        public Province GetProvince(ProvinceId id) => Provinces[id.Value];
        public City GetCity(CityId id) => Cities[id.Value];

        public float Dist(int a, int b)
        {
            float dx = Grid.X[a] - Grid.X[b], dy = Grid.Y[a] - Grid.Y[b];
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
