using System;
using System.Collections.Generic;

namespace Vigo.Core.CityBuilder
{
    public enum RoadType : byte { Local, Urban, Avenue, Highway }
    public enum Zone : byte { None, ResidentialLow, ResidentialHigh, Commercial, Industrial }
    public enum ServiceKind : byte { School, Hospital, Police, Park }
    public enum BuildingKind : byte { ResLow, ResHigh, Commercial, Industrial, School, Hospital, Police, Park }

    public static class RoadSpecs
    {
        public static float Width(RoadType t) { switch (t) { case RoadType.Local: return 8f; case RoadType.Urban: return 12f; case RoadType.Avenue: return 20f; default: return 28f; } }
        /// <summary>Độ sâu lô đất hai bên đường; cao tốc không có mặt tiền.</summary>
        public static float LotDepth(RoadType t) { switch (t) { case RoadType.Local: return 22f; case RoadType.Urban: return 26f; case RoadType.Avenue: return 30f; default: return 0f; } }
        public static float Speed(RoadType t) { switch (t) { case RoadType.Local: return 8f; case RoadType.Urban: return 12f; case RoadType.Avenue: return 16f; default: return 28f; } }
    }

    public static class BuildingSpecs
    {
        public const int MaxLevel = 3;
        public static BuildingKind KindOf(Zone z)
        {
            switch (z) { case Zone.ResidentialLow: return BuildingKind.ResLow; case Zone.ResidentialHigh: return BuildingKind.ResHigh; case Zone.Commercial: return BuildingKind.Commercial; default: return BuildingKind.Industrial; }
        }
        public static int Housing(BuildingKind k, int level) { return k == BuildingKind.ResLow ? 8 * level : (k == BuildingKind.ResHigh ? 36 * level : 0); }
        public static int Jobs(BuildingKind k, int level)
        {
            switch (k)
            {
                case BuildingKind.Commercial: return 6 * level; case BuildingKind.Industrial: return 10 * level;
                case BuildingKind.School: return 15; case BuildingKind.Hospital: return 25; case BuildingKind.Police: return 12; case BuildingKind.Park: return 2;
                default: return 0;
            }
        }
        public static bool IsService(BuildingKind k) { return k >= BuildingKind.School; }
        public static bool IsResidential(BuildingKind k) { return k == BuildingKind.ResLow || k == BuildingKind.ResHigh; }
        /// <summary>Bán kính phục vụ theo quãng đường đi trên mạng đường (m).</summary>
        public static float Reach(BuildingKind k) { switch (k) { case BuildingKind.School: return 600f; case BuildingKind.Hospital: return 900f; case BuildingKind.Police: return 700f; default: return 400f; } }
        /// <summary>Số cư dân một cơ sở phục vụ đủ tốt.</summary>
        public static float Capacity(BuildingKind k) { switch (k) { case BuildingKind.School: return 400f; case BuildingKind.Hospital: return 1500f; case BuildingKind.Police: return 1200f; default: return 600f; } }
    }

    public sealed class RoadNode
    {
        public int Id; public float X, Y; public bool IsExit;
        public List<int> Edges = new List<int>();
    }

    public sealed class RoadEdge
    {
        public int Id, A, B; public RoadType Type; public float Length; public bool Alive = true; public bool IsBridge;
    }

    public sealed class Parcel
    {
        public int Id; public bool Alive = true;
        public float X, Y;
        public int EdgeId, AccessNode; public float AccessDist;
        public Zone Zone; public int Building = -1;
    }

    public sealed class Building
    {
        public int Id; public bool Alive = true; public BuildingKind Kind; public int Parcel; public int Level = 1;
        public int Residents, Workers;
        public int Housing => BuildingSpecs.Housing(Kind, Level);
        public int Jobs => BuildingSpecs.Jobs(Kind, Level);
        public int BuiltWeek;
        // Kết quả phủ dịch vụ/môi trường (cập nhật mỗi tuần) — chỉ có nghĩa với nhà ở.
        public float CovEdu, CovHealth, CovSafety, CovLeisure, Pollution, CommuteDist, Quality;
    }

    public struct Result
    {
        public bool Ok; public string Error; public int Count;
        public static Result Success(int count = 0) { return new Result { Ok = true, Count = count }; }
        public static Result Fail(string e) { return new Result { Ok = false, Error = e }; }
    }

    public struct CityIndicators
    {
        public int Week, Population, Housing, Jobs, Employed, Buildings, ZonedParcels, EmptyZonedParcels, ConnectedParcels;
        public float UnemploymentRate, DemandR, DemandC, DemandI;
        /// <summary>Chất lượng sống 0..1 (trung bình theo dân cư) và nhóm 20% thấp nhất (TDD 7.5).</summary>
        public float QoL, QoLLowestQuintile;
        public float Employment, Health, Education, Safety, Commute, Environment, Leisure, Pollution, Crime;
    }
}
