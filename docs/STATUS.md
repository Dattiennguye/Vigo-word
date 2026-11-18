# VIGO – Trạng thái dự án & ghi chú bàn giao

File này để lần sau (người hoặc Claude) đọc là tiếp tục được ngay, không phải đọc lại toàn bộ mã.

## Quyết định đã chốt (từ Dat)
- 2 bản: Windows + Android. Game vô tận, KHÔNG có chiến thắng; thành công = chất lượng sống (QoL) của dân.
- Mất nước = game over (lãnh đạo bị xử lý), phải chơi lại; bắt buộc có hệ thống save/rewind.
- Nguyên tắc GDD: không dữ liệu giả, module nối bằng dữ liệu thật, mô phỏng phân cấp ưu tiên hiệu năng.

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

## Sau đó
GĐ4: AI quốc gia (ngân sách, mở rộng, ngoại giao), quân sự, chiến tranh dùng TransferProvince; nối QoL thành phố ↔ kinh tế quốc gia; hoàn thiện; Unity spike cho Windows+Android (không chạy được trong môi trường cloud).
