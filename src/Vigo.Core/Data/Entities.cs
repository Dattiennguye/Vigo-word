using System;
using System.Collections.Generic;

namespace Vigo.Core
{
    // ---- ID mạnh kiểu (TDD mục 4.1). Giá trị = chỉ số trong danh sách, không tái sử dụng. ----
    public readonly struct CountryId : IEquatable<CountryId>
    {
        public readonly int Value;
        public CountryId(int v) { Value = v; }
        public static readonly CountryId None = new CountryId(-1);
        public bool IsNone => Value < 0;
        public bool Equals(CountryId o) => Value == o.Value;
        public override bool Equals(object o) => o is CountryId c && c.Value == Value;
        public override int GetHashCode() => Value;
        public override string ToString() => "C" + Value;
        public static bool operator ==(CountryId a, CountryId b) => a.Value == b.Value;
        public static bool operator !=(CountryId a, CountryId b) => a.Value != b.Value;
    }

    public readonly struct ProvinceId : IEquatable<ProvinceId>
    {
        public readonly int Value;
        public ProvinceId(int v) { Value = v; }
        public static readonly ProvinceId None = new ProvinceId(-1);
        public bool IsNone => Value < 0;
        public bool Equals(ProvinceId o) => Value == o.Value;
        public override bool Equals(object o) => o is ProvinceId c && c.Value == Value;
        public override int GetHashCode() => Value;
        public override string ToString() => "P" + Value;
        public static bool operator ==(ProvinceId a, ProvinceId b) => a.Value == b.Value;
        public static bool operator !=(ProvinceId a, ProvinceId b) => a.Value != b.Value;
    }

    public readonly struct CityId : IEquatable<CityId>
    {
        public readonly int Value;
        public CityId(int v) { Value = v; }
        public static readonly CityId None = new CityId(-1);
        public bool IsNone => Value < 0;
        public bool Equals(CityId o) => Value == o.Value;
        public override bool Equals(object o) => o is CityId c && c.Value == Value;
        public override int GetHashCode() => Value;
        public override string ToString() => "T" + Value;
        public static bool operator ==(CityId a, CityId b) => a.Value == b.Value;
        public static bool operator !=(CityId a, CityId b) => a.Value != b.Value;
    }

    public enum Biome : byte
    {
        Ocean, Lake, Ice, Tundra, Taiga, Steppe, Grassland, Forest, Wetland,
        Desert, Savanna, TropicalForest, Highland, Mountain
    }

    /// <summary>Lương thực, nước, gỗ suy ra từ biome/thủy văn; các loại dưới đây là mỏ rời rạc.</summary>
    public enum ResourceType : byte
    {
        Coal, Oil, Gas, Iron, Copper, Aluminium, PreciousMetals, IndustrialMaterials
    }

    public enum Personality : byte
    {
        Trade, Industrial, Military, Technology, Neutral, Expansionist, Balanced
    }

    public enum FlagPattern : byte
    {
        HorizontalBands, VerticalBands, NordicCross, Diagonal, Canton, Disc, Chevron, Saltire
    }

    public struct FlagSpec
    {
        public FlagPattern Pattern;
        /// <summary>Màu RGB 0xRRGGBB: nền, phụ, nhấn.</summary>
        public int Primary, Secondary, Accent;
    }

    public enum RoadClass : byte { National, Highway }

    public sealed class Country
    {
        public CountryId Id;
        public string Name;
        public FlagSpec Flag;
        public int Culture;
        public Personality Personality;
        public int SeedCell;
        public CityId Capital = CityId.None;
        public ProvinceId CapitalProvince = ProvinceId.None;
        public List<ProvinceId> Provinces = new List<ProvinceId>();
        public List<CountryId> Neighbors = new List<CountryId>();
        public int LandCells;
        public long Population;
        public bool HasPort;
        /// <summary>Mất hết tỉnh = mất nước (game over nếu là nước của người chơi).</summary>
        public bool Eliminated;
        public float CentroidX, CentroidY;
    }

    public sealed class Province
    {
        public ProvinceId Id;
        public string Name;
        public CountryId Owner;
        public CountryId OriginalOwner;
        public int SeedCell;
        public int[] Cells;
        public List<CityId> Cities = new List<CityId>();
        public long Population;
        public float CentroidX, CentroidY;
        public float FoodPotential;
    }

    public sealed class City
    {
        public CityId Id;
        public string Name;
        public ProvinceId Province;
        public int Cell;
        public float X, Y;
        public long Population;
        public bool IsPort;
        public bool IsCapital;
        public bool IsProvincialSeat;
    }

    public sealed class ResourceDeposit
    {
        public ResourceType Type;
        public int Cell;
        public float Richness;
        public float Reserve;
        /// <summary>Mỏ ngoài khơi (dầu/khí thềm lục địa) — chưa thuộc quốc gia nào ở Giai đoạn 1.</summary>
        public bool Offshore;
    }

    public sealed class RoadSegment
    {
        public CityId A, B;
        public int[] Cells;
        public RoadClass Class;
        /// <summary>Ô mà đường cắt qua sông → cầu.</summary>
        public List<int> BridgeCells = new List<int>();
    }
}
