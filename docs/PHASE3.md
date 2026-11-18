# Giai đoạn 3 – Kinh tế (lõi mô phỏng)

Mã: `src/Vigo.Core/Economy/` (EconomyData, EconomyState, EconomyInit, EconomySim) và `src/Vigo.Core/Data/WorldOps.cs`.
Chạy thử: `dotnet run --project src/Vigo.Cli -- --econ 40 --seed 12345` (in bảng theo năm, sổ cái từng hàng hóa, nước thiếu ăn/hàng).
Test: `dotnet run --project src/Vigo.Tests` — 34 test đạt (11 test kinh tế/chiến tranh mới).

## Nguyên tắc GDD được giữ
- **Không dữ liệu giả**: mọi công suất sinh ra từ thế giới đã sinh — độ phì từng ô → nông nghiệp, biome rừng → gỗ, `ResourceDeposit` (kể cả ngoài khơi, gán cho tỉnh ven bờ gần nhất) → khai khoáng, dân tỉnh × tính cách quốc gia → công nghiệp.
- **Module nối bằng dữ liệu thật**: dân/lãnh thổ đọc từ `WorldState`; dân số kinh tế ghi ngược lại `Province.Population`.
- **Bảo toàn**: mọi hàng hóa đi qua sổ cái `Ledger`; `AuditGoods()` kiểm Σ tồn kho = đầu kỳ + sản xuất − tiêu dùng − chế biến − đầu tư − hao hụt/mất. Sai lệch đo được ~1e-14 sau 40 năm.

## Mô hình (mỗi tuần)
14 hàng hóa: thô (lương thực, gỗ, than, dầu, khí, sắt, đồng, nhôm, kim loại quý, vật liệu CN) và chế biến (thép, nhiên liệu, máy móc, hàng tiêu dùng) với công thức nhiều đầu vào (kiểu Leontief).

1. **Lao động**: 30% dân làm trong các ngành mô hình hóa; thiếu lao động → giảm công suất toàn tỉnh. Y tế nâng lao động/giảm tử vong, giáo dục nâng năng suất (0.8–1.2×), thuế > 25% bào mòn sản lượng.
2. **Sản xuất** thô → chế biến (Steel → Fuel → Machinery → Goods), dùng tồn kho quốc gia. Mỏ hao dần (`DepRemaining`), cạn là hết. Sản lượng tự giảm khi tồn kho vượt xa nhu cầu (điều tiết).
3. **Hộ gia đình**: lương thực, nhiên liệu, hàng tiêu dùng theo đầu người; thiếu lương thực → tử vong do đói, dân giảm.
4. **Hao hụt** (lương thực 0.4%/tuần, nhiên liệu 0.1%/tuần) — ghi sổ "Lost".
5. **Tài chính**: thuế = thuế suất × giá trị gia tăng; chia ngân sách giáo dục/y tế/đầu tư/quốc phòng. Đầu tư mua máy móc trong nước để mở rộng công suất (ưu tiên ngành có giá cao; có thể đặt tay). Công suất khấu hao 0.04%/tuần nên không đầu tư thì suy giảm.
6. **Giá** quốc gia theo độ che phủ tồn kho (khan → giá tăng tới 6×, dư → giảm).
7. **Thương mại thế giới**: nước dư xuất, nước thiếu nhập, chia theo tỉ lệ nguồn cung, bỏ qua cặp bị cấm vận/chiến tranh (`SetTradeBlock`). Hàng và tiền đều zero-sum (có test). Có hạn mức vay (10 tuần thuế tiềm năng); lương thực được nhập bằng vay cứu đói. Nợ = kho bạc âm, có lãi.
8. **Chiến tranh/mất nước**: `WorldOps.TransferProvince` chuyển tỉnh giữ bất biến phân cấp (chủ ô, danh sách tỉnh, dân, thủ đô, láng giềng, cảng); công suất đi theo tỉnh; hết tỉnh → `Eliminated` (nền tảng cho game over), tồn kho nước đó ghi vào "Lost". `DamageProvince` mô phỏng tàn phá.

## Số liệu đo được (20k ô, 30 nước)
- 15–40 năm mất < 1 giây; 1 tuần ≈ vài ms.
- 12 seed × 40 năm: năm 30 có 0–1/30 nước dưới 90% lương thực; nhiên liệu dao động 70–100% do mỏ dầu cạn dần; 5 seed × 40 năm không nước nào dưới 80% lương thực (test).
- Kịch bản: cấm vận + phá 90% đất canh tác → FoodSat <70%, giá lương thực >1.5×, dân giảm; mỏ dầu cạn → nhiên liệu <50%, giá >1.5×; thuế 50% thu nhiều hơn nhưng sản lượng thấp hơn; không đầu tư → công suất suy giảm.

## Hiệu chỉnh — nói thẳng
Các hằng số (nhu cầu/người, công thức, giá gốc, capex, khấu hao, hao hụt, hệ số thuế/giáo dục) là **hằng số mô hình do tôi đặt cho ổn định nội tại**, KHÔNG hiệu chỉnh theo kinh tế thực. Công suất khởi tạo được chuẩn hóa theo nhu cầu thế giới (lương thực ×1.25, còn lại ×1.3) rồi phân bố theo dữ liệu thế giới nên mất cân đối địa phương là thật, còn tổng cung–cầu là tham số.

Hai lỗi mô hình tìm ra khi chạy thật và đã sửa: (1) lương thực tồn đọng vô hạn → thêm hao hụt + giảm sản lượng khi dư; (2) nước công nghiệp thiếu nguyên liệu nhập rồi rơi vào bẫy nghèo vì nhà máy trả đầu vào bằng ngân sách thuế → thêm doanh thu bán hàng trang trải đầu vào trung gian, hạn mức vay và vay cứu đói.

## Hạn chế đã biết
- Chưa có tiền hộ gia đình/lương/giá cả lạm phát: tiền chỉ ở cấp quốc gia (kho bạc); tiền thuế "được tạo ra", chi tiêu và lãi nợ "bị hủy"; số dư quá lớn rò rỉ 2%/tuần (tham số).
- Thương mại chưa tính địa lý/vận tải/thuế quan/đường biển (nước nội địa nhập như nước biển); mỏ và hàng không hao hụt khi vận chuyển.
- Phân phối nội bộ: nạn đói chia đều theo quốc gia, chưa theo tỉnh/thành phố; chưa liên kết với `CitySim` (QoL thành phố chưa đọc các chỉ số này).
- Dân số chỉ sinh − tử, chưa di cư; dân thành phố (`City.Population`) chưa cập nhật theo tỉnh.
- Chưa có AI quản lý ngân sách: nước nghèo đất không tự cứu mình nên nợ chồng chất (sẽ do AI quốc gia ở giai đoạn sau).
- Quốc phòng mới chỉ ghi nhận mức chi (`DefenseSpending`); chưa có quân đội, bảo trì hay chiến phí.
- Chưa lưu/nạp trạng thái kinh tế (save/rewind còn ở TDD mục 12.4), RNG thời tiết là hàm của (seed, tuần) nên không cần lưu.
- Tất định đã kiểm trên cùng máy; chưa kiểm xuyên nền tảng (Windows/Android) — dùng `double`, cần thận trọng với thứ tự cộng dồn.
