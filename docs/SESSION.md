# GameSession – lớp lệnh/vòng lặp thống nhất cho lớp Unity

`Vigo.Core.GameSession` gom mọi module (thế giới, kinh tế, chiến tranh, AI ngân sách, AI ngoại giao, thành phố chi tiết, quay lại) sau một API. Lớp Unity chỉ cần gọi lớp này và đọc `Status()`; không đụng trực tiếp vào các sim.

## Vòng lặp
```csharp
var game = GameSession.New(new WorldConfig { Seed = 12345, CellCount = 60000 }, playerCountryId);
var city = game.OpenCity(game.World.GetCountry(game.Player).Capital);   // mô phỏng chi tiết thủ đô
// mỗi tick (ví dụ 1 tuần game = 1–2 giây): 
if (!game.StepWeek()) { /* GameOver: mất nước */ }
var st = game.Status();   // tuần, dân, ngân khố, QoL TB các thành phố, số chiến tranh, đề nghị đang chờ...
```
Thứ tự mỗi tuần: kinh tế → chiến tranh → AI ngân sách → AI ngoại giao → thành phố (nhận bối cảnh quốc gia, chạy, báo ngược). Tự lưu mốc quay lại mỗi `AutosaveEvery` (13) tuần, giữ 40 mốc.

## Lệnh của người chơi (chỉ tác động nước mình)
`SetTax`, `SetBudget`, `DeclareWar`, `ProposePeace`, `ProposeAlliance`, `ProposePact`, `ProposeTrade`, `BreakAlliance`, `Inbox()` + `AcceptOffer/DeclineOffer(id)`, `OpenCity(cityId)`. Lệnh xây trong thành phố gọi trực tiếp `CitySim` (BuildRoad, Zone…) qua `game.Cities`.

## Lưu / nạp / quay lại
`ToBytes/Save(path)`, `GameSession.FromBytes/Load(path)`, `Rewind(week)`. Nạp rồi chạy tiếp cho kết quả y hệt chạy liền (test), kể cả thành phố, chiến tranh, đề nghị.

## Lỗi tìm thấy nhờ test của lớp này
Ảnh hưởng ngược của thành phố (`CityLabourMod/CityGrowthAdj`) không nằm trong bản lưu → sau khi nạp tuần đầu bị lệch so với chạy liền. Sửa: `GameSession` dựng lại từ QoL hiện tại khi nạp/quay lại. Cũng sửa trước đó: `TradePref` tính ngay trước giao thương thay vì lưu.

## Hạn chế
- Cấu hình AI (Austerity, Aggression…) quay về mặc định khi nạp (không lưu).
- Một người chơi; chưa có nhiều người chơi/hotseat.
- Chưa có giao diện/đồ họa: Unity spike chưa làm (cần máy có Unity).
