using System;
using System.Collections.Generic;
using System.IO;

namespace Vigo.Core.CityBuilder
{
    /// <summary>Một thành phố chi tiết gắn với thành phố của thế giới (CityId) để lưu cùng bản lưu.</summary>
    public sealed class CityEntry
    {
        public CityId Id; public CitySim Sim;
        public CityEntry(CityId id, CitySim sim) { Id = id; Sim = sim; }
    }

    public sealed partial class CitySim
    {
        /// <summary>Ghi toàn bộ trạng thái mô phỏng (không gồm bối cảnh quốc gia — CityLink áp lại mỗi tuần).</summary>
        public void WriteState(BinaryWriter b, ulong seedForRng = 0)
        {
            b.Write(Size); b.Write(Week); b.Write(_unemployment);
            var st = _rng.State; foreach (ulong v in st) b.Write(v);
            b.Write(Nodes.Count);
            foreach (var n in Nodes) { b.Write(n.X); b.Write(n.Y); b.Write(n.IsExit); b.Write(n.Edges.Count); foreach (int e in n.Edges) b.Write(e); }
            b.Write(Edges.Count);
            foreach (var e in Edges) { b.Write(e.A); b.Write(e.B); b.Write((byte)e.Type); b.Write(e.Length); b.Write(e.Alive); b.Write(e.IsBridge); }
            b.Write(Parcels.Count);
            foreach (var p in Parcels) { b.Write(p.Alive); b.Write(p.X); b.Write(p.Y); b.Write(p.EdgeId); b.Write(p.AccessNode); b.Write(p.AccessDist); b.Write((byte)p.Zone); b.Write(p.Building); }
            b.Write(Buildings.Count);
            foreach (var x in Buildings)
            {
                b.Write(x.Alive); b.Write((byte)x.Kind); b.Write(x.Parcel); b.Write(x.Level); b.Write(x.Residents); b.Write(x.Workers); b.Write(x.BuiltWeek);
                b.Write(x.CovEdu); b.Write(x.CovHealth); b.Write(x.CovSafety); b.Write(x.CovLeisure); b.Write(x.Pollution); b.Write(x.CommuteDist); b.Write(x.Quality);
            }
            var i = Indicators;
            b.Write(i.Week); b.Write(i.Population); b.Write(i.Housing); b.Write(i.Jobs); b.Write(i.Employed); b.Write(i.Buildings); b.Write(i.ZonedParcels); b.Write(i.EmptyZonedParcels); b.Write(i.ConnectedParcels);
            b.Write(i.UnemploymentRate); b.Write(i.DemandR); b.Write(i.DemandC); b.Write(i.DemandI); b.Write(i.QoL); b.Write(i.QoLLowestQuintile);
            b.Write(i.Employment); b.Write(i.Health); b.Write(i.Education); b.Write(i.Safety); b.Write(i.Commute); b.Write(i.Environment); b.Write(i.Leisure); b.Write(i.Pollution); b.Write(i.Crime);
        }

        /// <summary>Dựng lại thành phố từ trạng thái đã ghi, trên địa hình đã dựng lại (xem SaveGame).</summary>
        public static CitySim ReadState(BinaryReader r, ITerrain terrain)
        {
            float size = r.ReadSingle();
            var sim = new CitySim(terrain, 0, size);
            sim.Nodes.Clear();
            sim.Week = r.ReadInt32(); sim._unemployment = r.ReadSingle();
            sim._rng.State = new[] { r.ReadUInt64(), r.ReadUInt64(), r.ReadUInt64(), r.ReadUInt64() };
            int nn = Count(r, 5_000_000);
            for (int k = 0; k < nn; k++)
            {
                var n = new RoadNode { Id = k, X = r.ReadSingle(), Y = r.ReadSingle(), IsExit = r.ReadBoolean() };
                int ne = Count(r, 1000); for (int j = 0; j < ne; j++) n.Edges.Add(r.ReadInt32());
                sim.Nodes.Add(n);
            }
            int nE = Count(r, 5_000_000);
            for (int k = 0; k < nE; k++) sim.Edges.Add(new RoadEdge { Id = k, A = r.ReadInt32(), B = r.ReadInt32(), Type = (RoadType)r.ReadByte(), Length = r.ReadSingle(), Alive = r.ReadBoolean(), IsBridge = r.ReadBoolean() });
            int nP = Count(r, 5_000_000);
            for (int k = 0; k < nP; k++)
                sim.Parcels.Add(new Parcel { Id = k, Alive = r.ReadBoolean(), X = r.ReadSingle(), Y = r.ReadSingle(), EdgeId = r.ReadInt32(), AccessNode = r.ReadInt32(), AccessDist = r.ReadSingle(), Zone = (Zone)r.ReadByte(), Building = r.ReadInt32() });
            int nB = Count(r, 5_000_000);
            for (int k = 0; k < nB; k++)
                sim.Buildings.Add(new Building
                {
                    Id = k, Alive = r.ReadBoolean(), Kind = (BuildingKind)r.ReadByte(), Parcel = r.ReadInt32(), Level = r.ReadInt32(), Residents = r.ReadInt32(), Workers = r.ReadInt32(), BuiltWeek = r.ReadInt32(),
                    CovEdu = r.ReadSingle(), CovHealth = r.ReadSingle(), CovSafety = r.ReadSingle(), CovLeisure = r.ReadSingle(), Pollution = r.ReadSingle(), CommuteDist = r.ReadSingle(), Quality = r.ReadSingle()
                });
            foreach (var p in sim.Parcels)
            {
                if (p.EdgeId < 0 || p.EdgeId >= sim.Edges.Count || p.AccessNode < 0 || p.AccessNode >= sim.Nodes.Count || p.Building >= sim.Buildings.Count) throw new InvalidDataException("tham chiếu lô đất sai");
                int cx = (int)Math.Floor(p.X / 20f), cy = (int)Math.Floor(p.Y / 20f);
                if (!sim._parcelHash.TryGetValue(HashKey(cx, cy), out var list)) { list = new List<int>(); sim._parcelHash[HashKey(cx, cy)] = list; }
                list.Add(p.Id);
            }
            foreach (var b in sim.Buildings) if (b.Parcel < 0 || b.Parcel >= sim.Parcels.Count) throw new InvalidDataException("tham chiếu công trình sai");
            foreach (var e in sim.Edges) if (e.A < 0 || e.A >= sim.Nodes.Count || e.B < 0 || e.B >= sim.Nodes.Count) throw new InvalidDataException("tham chiếu đường sai");
            sim.Indicators = new CityIndicators
            {
                Week = r.ReadInt32(), Population = r.ReadInt32(), Housing = r.ReadInt32(), Jobs = r.ReadInt32(), Employed = r.ReadInt32(), Buildings = r.ReadInt32(), ZonedParcels = r.ReadInt32(), EmptyZonedParcels = r.ReadInt32(), ConnectedParcels = r.ReadInt32(),
                UnemploymentRate = r.ReadSingle(), DemandR = r.ReadSingle(), DemandC = r.ReadSingle(), DemandI = r.ReadSingle(), QoL = r.ReadSingle(), QoLLowestQuintile = r.ReadSingle(),
                Employment = r.ReadSingle(), Health = r.ReadSingle(), Education = r.ReadSingle(), Safety = r.ReadSingle(), Commute = r.ReadSingle(), Environment = r.ReadSingle(), Leisure = r.ReadSingle(), Pollution = r.ReadSingle(), Crime = r.ReadSingle()
            };
            sim.ComputeConnectivity();
            return sim;
        }

        private static int Count(BinaryReader r, int max) { int n = r.ReadInt32(); if (n < 0 || n > max) throw new InvalidDataException("kích thước thành phố không hợp lệ"); return n; }
    }
}
