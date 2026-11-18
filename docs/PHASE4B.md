# Giai đoạn 4b – Quân đội, ngoại giao, chiến tranh

Mã: `src/Vigo.Core/Military/WarSim.cs`, `DiplomacyAI.cs`; nối với `EconomySim` (quân số, hậu cần), `CountryAI` (ngân sách thời chiến), `SaveGame` (phiên bản 2).
Test: `src/Vigo.Tests/MilitaryTests.cs` (10 test; tổng 52 đạt). Chạy thử: `dotnet run --project src/Vigo.Cli -- --war 40 --seed 12345`.

## Cơ chế
- **Quân số** theo ngân sách quốc phòng (4 tiền/lính/tuần), tăng tối đa 1.5% dân/tuần, giải ngũ nhanh hơn, trần 12% dân. Quân nhân bị trừ khỏi lao động → sản lượng giảm.
- **Hậu cần thật**: mỗi lính tiêu nhiên liệu và máy móc từ tồn kho quốc gia (ghi `Ledger.Military`, sổ cái vẫn bảo toàn ~1e-14). Hết hàng → hệ số hậu cần giảm → sức mạnh giảm. Sức mạnh = quân × (0.7 + 0.6 × giáo dục) × hậu cần.
- **Tuyên chiến** chỉ với láng giềng còn sống; tự động cấm vận hai chiều (dùng `SetTradeBlock`), hòa thì gỡ.
- **Chiến sự theo tỉnh tiền tuyến**: lực lượng chia đều cho các tỉnh giáp địch; phòng thủ có thưởng địa hình (đồi núi) và 15% lợi thế. Mỗi tuần tổn thất hai bên theo tỉ lệ lực, tỉnh tiền tuyến bị phá 1% công suất và 0.4% dân. Tiến độ chiếm tích lũy chậm (tối thiểu ~17 tuần/tỉnh); đủ thì `WorldOps.TransferProvince`. Tỉnh vừa chiếm mất 40% công suất và 5% dân.
- **Mục tiêu chiến tranh**: mỗi cuộc chiến có mục tiêu = max(2, 25% số tỉnh đối phương) (thủ đô tính 3); đạt → hòa. Ngoài ra hòa khi một bên kiệt sức (<35% lực đối phương sau 26 tuần) hoặc kéo dài >260 tuần.
- **Người chơi**: `DeclareWar`, `MakePeace`, `ProposePeace` (AI từ chối nếu đang áp đảo >1.5× và chiến tranh <52 tuần), `GameOver(human)` = mất hết tỉnh. Nhật ký sự kiện (`Events`) cho giao diện.
- **AI ngoại giao**: chỉ tính cách Quân sự/Bành trướng chủ động tuyên chiến, mỗi 13 tuần xác suất 25% (tất định theo seed/tuần/nước), nhắm láng giềng yếu hơn ≥1.8×, nghỉ 156 tuần sau mỗi cuộc chiến, cooldown 104 tuần với cùng một đối thủ. Thời chiến, AI ngân sách chuyển sang quốc phòng 45%.
- **Lưu/nạp** đầy đủ giữa chiến tranh (quân, tiến độ chiếm, mục tiêu, sự kiện, thời điểm nghỉ): nạp rồi chạy tiếp giống hệt chạy liền.

## Đo được (20k ô, 30 nước, chỉ 2/7 tính cách hiếu chiến)
60 năm mất ~1.2 giây. Còn lại sau 60 năm: seed 12345 → 10/30 nước (77 cuộc chiến), seed 777 → 18/30 (65), seed 31337 → 11/30 (80). Thế giới hợp nhất dần chứ không sụp đổ; mọi bất biến phân cấp (chủ ô, danh sách tỉnh, thủ đô trong lãnh thổ) và sổ cái giữ nguyên.

## Lỗi cân bằng tìm ra khi chạy thật và đã sửa
Lần chạy đầu thế giới sụp còn 1–2 nước sau 25–35 năm, một nước chiếm 8 tỉnh trong 3 tuần (hiệu ứng quả cầu tuyết). Đã sửa bằng: chiếm chậm hơn ~4×, mục tiêu chiến tranh, tàn phá tỉnh bị chiếm, nghỉ sau chiến tranh.

## Hạn chế — nói thẳng
- Các hằng số chiến đấu (tổn thất 3%, thưởng địa hình, tốc độ chiếm) là **tham số tôi đặt**, chưa hiệu chỉnh/cân bằng theo trải nghiệm chơi; mức độ "hợp nhất còn 10–18 nước sau 60 năm" là kết quả của tham số đó chứ không phải dự báo.
- Chiến đấu trừu tượng (không có đơn vị di chuyển, không bản đồ trận), chưa có hải quân/không quân, chưa có liên minh/hiệp ước/đồng minh tham chiến, chưa có hòa đàm có điều kiện (đổi tỉnh, bồi thường).
- Mục tiêu chiến tranh và việc xin hòa của AI còn đơn giản; AI không tính tới đồng minh, uy tín hay dư luận.
- Quân đội chưa có loại binh chủng/trang bị; chất lượng chỉ qua giáo dục và hậu cần.
- Người chơi chưa có lệnh chỉ huy chiến dịch (chọn mặt trận ưu tiên) — mới ở mức tuyên chiến/hòa/ngân sách.
- Chưa nối thành phố chi tiết (CitySim) với chiến tranh: tỉnh bị chiếm chưa ảnh hưởng thành phố người chơi đang xây.
