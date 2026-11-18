# Giai đoạn 5 – Nối thành phố chi tiết với quốc gia

Mã: `src/Vigo.Core/City/CityLink.cs`, `NationalContext` (CityTypes.cs), thay đổi trong `CitySimStep.cs`, `WarSim.Devastation/CapturedWeek`, `SaveGame` (phiên bản 4).
Test: `src/Vigo.Tests/CityLinkTests.cs` (6 test; tổng 66 đạt).

## Cách dùng
Mỗi tuần: `eco.StepWeek(); war.StepWeek(); link.Apply(citySim, cityId); citySim.StepWeek();` với `link = new CityLink(world, eco, war)`.
`CitySim.National` = null thì thành phố chạy độc lập y như GĐ2 (không đổi hành vi).

## Quốc gia tác động thành phố thế nào (một chiều quốc gia → thành phố)
- **Mức sống quốc gia** (25% QoL): lương thực 50%, nhiên liệu 20%, hàng tiêu dùng 30% — lấy từ `Indicators` thật của nước sở hữu.
- **Nạn đói phi tuyến**: dưới 85% khẩu phần, QoL bị nhân 0.4–1.0 (0% ăn → ×0.4). Dân bỏ đi khi QoL < 0.30 nên nạn đói thật sự làm thành phố mất dân.
- **Chất lượng dịch vụ quốc gia**: độ phủ y tế/giáo dục của thành phố nhân với (0.5 + 0.5 × chỉ số y tế/giáo dục quốc gia từ ngân sách). Nhà nước cắt ngân sách thì bệnh viện/trường của thành phố kém đi dù xây đủ.
- **Tàn phá chiến tranh** (`WarSim.Devastation`): tỉnh vừa bị chiếm (0.6 giảm tuyến tính trong 52 tuần) hoặc đang là tiền tuyến/bị tấn công (0.3–1.0) → tăng tội phạm (+0.3 × mức tàn phá), giảm an ninh.
- **Đổi chủ**: `CityLink` tra lại chủ sở hữu mỗi tuần nên thành phố bị chiếm chịu kinh tế của chủ mới.

## Kết quả đo được
- Nạn đói giả lập (30% ăn): QoL 0.55 → 0.30, dân 2023 → 1748 sau 3 năm.
- Nạn đói thật (cấm vận + phá 90% đất canh tác, thành phố thủ đô): QoL 0.54 → 0.29.

## Lỗi mô hình tìm ra khi chạy thật và đã sửa
Lần đầu mức sống quốc gia chỉ cộng tuyến tính: ăn 30% mà QoL chỉ giảm 0.09 và dân không bỏ đi (QoL vẫn >0.30). Test đòi hỏi nạn đói phải gây mất dân và đã bắt được lỗi; sửa bằng hệ số phạt phi tuyến khi thiếu ăn chứ không nới test.

## Hạn chế — nói thẳng
- **Một chiều**: thành phố chưa tác động ngược lên quốc gia (thuế, lao động, sản xuất của thành phố chưa vào `EconomySim`; dân thành phố chưa đồng bộ với `Province.Population`).
- Trọng số 25%, ngưỡng 85%, hệ số 0.4 là tham số tôi đặt, chưa hiệu chỉnh theo trải nghiệm chơi.
- `CitySim` vẫn chưa được lưu trong `SaveGame` (chỉ lưu thế giới, kinh tế, chiến tranh) — thành phố chi tiết sẽ mất khi nạp.
- Mỗi lần chỉ nối một thành phố chi tiết; thành phố còn lại ở mức tổng hợp (`City.Population`) và chưa có mô phỏng chi tiết.
- Chưa có thu nhập/giá cả hộ gia đình trong QoL, chưa có nhà ở giá rẻ/nước sạch (như đã ghi ở TDD mục 7.5).
