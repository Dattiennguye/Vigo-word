using System;
using System.Collections.Generic;

namespace Vigo.Core.Economy
{
    /// <summary>Sổ cái hàng hóa toàn thế giới: Σ tồn kho = Initial + Produced − Household − Industry − Invested − Military − Lost.</summary>
    public sealed class Ledger
    {
        public double[] Initial = new double[EconData.GoodCount], Produced = new double[EconData.GoodCount],
            Household = new double[EconData.GoodCount], Industry = new double[EconData.GoodCount],
            Invested = new double[EconData.GoodCount], Military = new double[EconData.GoodCount], Lost = new double[EconData.GoodCount],
            TradeVolume = new double[EconData.GoodCount];
        /// <summary>Σ |tiền trả − tiền nhận| trong giao thương (phải ≈ 0).</summary>
        public double TradeMoneyError;
    }

    public struct CountryIndicators
    {
        public double Population, ValueAdded, TaxRevenue, Treasury;
        public double FoodSat, FuelSat, GoodsSat, Edu, Health, LabourUtil, PriceIndex;
    }

    public sealed class EconomyState
    {
        public int Week;
        // ---- Quốc gia ----
        public double[][] Stock, Price;
        public double[] Treasury, TaxRate, ShareEdu, ShareHealth, ShareInvest, ShareDefense, Edu, Health;
        public double[][] InvestWeight;      // [quốc gia][ngành] — null = tự động theo áp lực giá
        public bool[] Dead;
        public double[] Troops;              // quân số đang tại ngũ (là một phần dân số, trừ khỏi lao động)
        public double[] DefenseSpending;     // tiền chi quốc phòng tuần gần nhất (dành cho GĐ quân sự)
        public CountryIndicators[] Indicators;
        // ---- Tỉnh ----
        public double[] Pop, FarmCap, ForestCap, MineBase, MineBonus;
        public double[][] IndCap;            // [tỉnh][4 ngành chế biến]
        public double[] Util;
        // ---- Mỏ ----
        public int[] DepProvince; public double[] DepRate, DepRemaining;
        public List<int>[] DepsOfProvince;
        // ---- Khác ----
        public HashSet<long> Blocked = new HashSet<long>(); // cấm vận / chiến tranh (a*10000+b, đối xứng)
        public Ledger Ledger = new Ledger();
    }
}
