# VIGO – Trạng thái dự án & ghi chú bàn giao

File này để lần sau (người hoặc Claude) đọc là tiếp tục được ngay, không phải đọc lại toàn bộ mã.

## Quyết định đã chốt (từ Dat)
- 2 bản: Windows + Android. Game vô tận, KHÔNG có chiến thắng; thành công = chất lượng sống (QoL) của dân.
- Mất nước = game over (lãnh đạo bị xử lý), phải chơi lại; bắt buộc có hệ thống save/rewind.
- Nguyên tắc GDD: không dữ liệu giả, module nối bằng dữ liệu thật, mô phỏng phân cấp ưu tiên hiệu năng.

## Định hướng đồ họa (Dat, 2026-11-18)
Chỉ cần kiểu isometric hoạt hình, màu tươi, khối đơn giản như game SLG mobile (ảnh mẫu quảng cáo "Kỷ Nguyên Băng Hà: 3Q" làm THAM KHẢO phong cách, không sao chép tài sản/nhân vật của game đó). Phù hợp Android tầm trung: low-poly/sprite isometric, ít chi tiết, ưu tiên hiệu năng. Việc dựng đồ họa nằm ở lớp Unity (chưa làm); lõi mô phỏng không phụ thuộc đồ họa.

## Đã xong GĐ1–2 (đã push)
- GĐ1 World Generator (docs/PHASE1.md), GĐ2 City Builder (docs/PHASE2.md). TDD: docs/TDD.md.
- Lệnh: `cd src && dotnet run --project Vigo.Tests` (thêm `--seeds N`); `dotnet run --project Vigo.Cli -- --seed S --out out [--city capital|port]`.
- Môi trường: .NET 8; cần `nuget.config` cục bộ có `<clear/>` (đã gitignore) vì NuGet bị chặn.

## API cần nhớ
- `WorldGenerator.Generate(WorldConfig)` → `WorldState` (mảng theo ô: Elevation, Fertility, Biomes, CountryOfCell, ProvinceOfCell...; danh sách Countries/Provinces/Cities/Deposits/Roads).
- `Rng.Stream(seed, "tên", salt)` cho RNG tất định theo bước. ID kiểu mạnh CountryId/ProvinceId/CityId.
- Country: Population, Provinces, Neighbors, Personality. Province: Owner, OriginalOwner, Cells, Cities, FoodPotential. ResourceDeposit: Type, Cell, Reserve, Richness, Offshore.
- Test: thêm vào `CityTests.Register(add, assert)` hoặc file test mới đăng ký tương tự trong Program.cs.
- Namespace city: `Vigo.Core.CityBuilder`.

## GĐ3 – Kinh tế (XONG, 34 test pass) — xem docs/PHASE3.md
Mã: src/Vigo.Core/Economy/*, Data/WorldOps.cs (TransferProvince, Country.Eliminated). CLI: `--econ N`.
API: `new EconomySim(world)`, `StepWeek()`, `SetTaxRate`, `SetBudget`, `SetInvestWeights`, `SetTradeBlock`, `DamageProvince`, `AuditGoods()`, `S.Indicators[c]` (FoodSat, FuelSat, GoodsSat, Edu, Health, ValueAdded...).
Chưa nối với CitySim/QoL; chưa có AI ngân sách (nước nghèo đất nợ chồng chất); hằng số chưa hiệu chỉnh thực tế.

## Lưu/nạp/quay lại (XONG, 37 test pass) — xem docs/SAVE.md
`SaveGame.ToBytes/FromBytes/Save/Load(world, eco)`, `RewindBuffer(n).Push/Rewind(week)`. Chưa lưu CitySim.

## GĐ4a – AI quốc gia (XONG, 42 test pass) — xem docs/PHASE4A.md
`new CountryAI(eco)`, gọi `ai.Think()` sau mỗi `eco.StepWeek()`; `SetHuman(c,true)` cho nước người chơi. Phát hiện: tăng thuế chữa nợ phản tác dụng; AI chưa "tối ưu hơn" chính sách cố định, chỉ đa dạng hành vi. Tiếp theo (4b): quân đội, ngoại giao, chiến tranh, mở rộng.

## GĐ4b – Quân đội, ngoại giao, chiến tranh (XONG, 52 test pass) — xem docs/PHASE4B.md
`new WarSim(world, eco)`; mỗi tuần: `eco.StepWeek(); war.StepWeek(); ai.Think(); dip.Think();` (`new CountryAI(eco){War=war}`, `new DiplomacyAI(war)`). Lệnh: `DeclareWar/MakePeace/ProposePeace`, `GameOver(human)`, `Events`. SaveGame.ToBytes(w, eco, war) (phiên bản 2). Hằng số chiến đấu chưa cân bằng theo trải nghiệm chơi.

## Sau đó
GĐ4c: liên minh/hiệp ước/hòa đàm có điều kiện, hải quân; nối QoL thành phố ↔ kinh tế quốc gia; lệnh chỉ huy chiến dịch; command layer thống nhất; hoàn thiện; Unity spike cho Windows+Android (không chạy được trong môi trường cloud).
