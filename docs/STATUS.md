# VIGO – Trạng thái dự án & ghi chú bàn giao

File này để lần sau (người hoặc Claude) đọc là tiếp tục được ngay, không phải đọc lại toàn bộ mã.

## Quyết định đã chốt (từ Dat)
- 2 bản: Windows + Android. Game vô tận, KHÔNG có chiến thắng; thành công = chất lượng sống (QoL) của dân.
- Mất nước = game over (lãnh đạo bị xử lý), phải chơi lại; bắt buộc có hệ thống save/rewind.
- Nguyên tắc GDD: không dữ liệu giả, module nối bằng dữ liệu thật, mô phỏng phân cấp ưu tiên hiệu năng.

## Đã xong (đã push, 23 test pass)
- GĐ1 World Generator (docs/PHASE1.md), GĐ2 City Builder (docs/PHASE2.md). TDD: docs/TDD.md.
- Lệnh: `cd src && dotnet run --project Vigo.Tests` (thêm `--seeds N`); `dotnet run --project Vigo.Cli -- --seed S --out out [--city capital|port]`.
- Môi trường: .NET 8; cần `nuget.config` cục bộ có `<clear/>` (đã gitignore) vì NuGet bị chặn.

## API cần nhớ
- `WorldGenerator.Generate(WorldConfig)` → `WorldState` (mảng theo ô: Elevation, Fertility, Biomes, CountryOfCell, ProvinceOfCell...; danh sách Countries/Provinces/Cities/Deposits/Roads).
- `Rng.Stream(seed, "tên", salt)` cho RNG tất định theo bước. ID kiểu mạnh CountryId/ProvinceId/CityId.
- Country: Population, Provinces, Neighbors, Personality. Province: Owner, OriginalOwner, Cells, Cities, FoodPotential. ResourceDeposit: Type, Cell, Reserve, Richness, Offshore.
- Test: thêm vào `CityTests.Register(add, assert)` hoặc file test mới đăng ký tương tự trong Program.cs.
- Namespace city: `Vigo.Core.CityBuilder`.

## GĐ3 – Kinh tế (CHƯA code, chưa commit)
Thiết kế: namespace `Vigo.Core.Economy`, các file EconomyData / EconomyState / EconomyInit / EconomySim / Commands; `WorldOps.TransferProvince` (+ `Country.Eliminated`); CLI `--econ`.
- Hàng hóa + công thức sản xuất, năng lực theo tỉnh từ dữ liệu thế giới (đất đai, mỏ, dân/lao động).
- Vòng tuần: sản xuất → tiêu dùng hộ gia đình → giá → thương mại thế giới → ngân sách/thuế → sổ cái bảo toàn (kiểm toán hàng hóa).
- Test: bảo toàn, thương mại tổng bằng 0, tất định, soak 30 nước, thuế/giáo dục/khan hiếm/cạn mỏ/đầu tư/chiến tranh/giá/nạn đói.
- Việc còn lại: tasks #9–#12 (data+init, vòng tuần, TransferProvince+test, hiệu chỉnh + PHASE3.md + commit/push + cập nhật memory).

## Sau đó
AI quốc gia, quân sự, ngoại giao, chiến tranh, hoàn thiện; Unity spike cho Windows+Android (không chạy được trong môi trường cloud).
