using System;

namespace Vigo.Core.Economy
{
    public enum Good { Food, Wood, Coal, Oil, Gas, Iron, Copper, Aluminium, Precious, IndMat, Steel, Fuel, Machinery, Goods }
    public enum Sector { Farm, Forestry, Mining, Steel, Fuel, Machinery, Goods }

    /// <summary>Hằng số kinh tế. Toàn bộ là hằng số MÔ HÌNH chưa hiệu chỉnh theo dữ liệu thực (xem docs/PHASE3.md).</summary>
    public static class EconData
    {
        public const int GoodCount = 14;
        public const int SectorCount = 7;
        public const int IndustrySectors = 4; // Steel, Fuel, Machinery, Goods

        public static readonly double[] BasePrice = { 1, 1.2, 1.5, 3, 2.5, 2, 6, 4, 40, 3, 5, 5, 18, 8 };

        /// <summary>Nhu cầu hộ gia đình / người / tuần. 1 đơn vị lương thực = nuôi 1 người 1 tuần.</summary>
        public static readonly double[] HouseholdNeed = MakeNeed();
        private static double[] MakeNeed() { var a = new double[GoodCount]; a[(int)Good.Food] = 1.0; a[(int)Good.Fuel] = 0.10; a[(int)Good.Goods] = 0.12; return a; }

        /// <summary>Máy móc cần cho đầu tư công mỗi đầu người mỗi tuần (dùng để ước nhu cầu khi khởi tạo).</summary>
        public const double MachineryPerCapita = 0.008;

        public sealed class Recipe
        {
            public Good Out; public Good[] In; public double[] Qty;
            public Recipe(Good o, Good[] i, double[] q) { Out = o; In = i; Qty = q; }
        }

        /// <summary>Công thức cho 4 ngành chế biến, chỉ số = Sector - Steel.</summary>
        public static readonly Recipe[] Industry =
        {
            new Recipe(Good.Steel,     new[] { Good.Iron, Good.Coal },                                   new[] { 1.0, 0.5 }),
            new Recipe(Good.Fuel,      new[] { Good.Oil, Good.Gas },                                     new[] { 1.0, 0.2 }),
            new Recipe(Good.Machinery, new[] { Good.Steel, Good.Copper, Good.Aluminium },                new[] { 0.6, 0.2, 0.2 }),
            new Recipe(Good.Goods,     new[] { Good.Steel, Good.IndMat, Good.Wood, Good.Precious, Good.Copper }, new[] { 0.3, 0.2, 0.3, 0.02, 0.1 }),
        };

        /// <summary>Số lao động (đơn vị người) cho 1 đơn vị công suất/tuần.</summary>
        public static readonly double[] LabourPerUnit = { 0.14, 0.05, 0.04, 0.05, 0.03, 0.10, 0.12 };
        /// <summary>Máy móc cần để thêm 1 đơn vị công suất/tuần.</summary>
        public static readonly double[] CapexPerUnit = { 5, 5, 8, 10, 10, 15, 10 };
        /// <summary>Hao hụt tồn kho mỗi tuần (hỏng/thất thoát) — ghi vào sổ cái 'Lost'.</summary>
        public static readonly double[] Spoilage = MakeSpoil();
        private static double[] MakeSpoil() { var a = new double[GoodCount]; a[(int)Good.Food] = 0.004; a[(int)Good.Fuel] = 0.001; return a; }
        public const double Depreciation = 0.0004;     // /tuần
        public const double LabourShare = 0.30;        // tỷ lệ dân làm trong các ngành được mô hình hóa
        public const double CapacityMargin = 1.3;      // công suất khởi tạo so với nhu cầu thế giới
        public const double FoodMargin = 1.25;

        public static Good ToGood(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Coal: return Good.Coal;
                case ResourceType.Oil: return Good.Oil;
                case ResourceType.Gas: return Good.Gas;
                case ResourceType.Iron: return Good.Iron;
                case ResourceType.Copper: return Good.Copper;
                case ResourceType.Aluminium: return Good.Aluminium;
                case ResourceType.PreciousMetals: return Good.Precious;
                default: return Good.IndMat;
            }
        }

        public static bool IsRaw(Good g) => (int)g < (int)Good.Steel;
    }
}
