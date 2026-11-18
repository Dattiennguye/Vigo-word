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

## Chiều ngược thành phố → quốc gia (GĐ6a)
Mỗi tuần: `link.ResetReports(); … citySim.StepWeek(); link.Report(citySim, cityId); eco.StepWeek();`
- `Report` lấy QoL thật của thành phố, quy về −1..+1, nhân với **tỷ trọng thật** của thành phố trong tỉnh (`City.Population / dân tỉnh`) rồi cộng vào `EconomySim.CityLabourMod[p]` (tối đa ±30% năng suất lao động) và `CityGrowthAdj[p]` (tối đa ±0,1%/tuần tăng dân). Nhiều thành phố cùng tỉnh cộng dồn.
- Hai mảng này không lưu (đặt lại mỗi tuần), giống `NationalContext`.
- Đo được (thành phố chiếm 50% tỉnh, 3 năm): dân tỉnh 34932 (không báo cáo) → 37082 (QoL 0,9) / 32783 (QoL 0,1). QoL 0,5 y hệt không báo cáo (test).
- Chưa thấy hiệu ứng năng suất trong test: tỉnh thử nghiệm không thiếu lao động (Util = 1,00) nên hệ số lao động chưa bộc lộ; tác động hiện đo được chủ yếu qua dân số. Thuế/sản xuất riêng của thành phố vẫn chưa nối.

## Kết quả đo được
- Nạn đói giả lập (30% ăn): QoL 0.55 → 0.30, dân 2023 → 1748 sau 3 năm.
- Nạn đói thật (cấm vận + phá 90% đất canh tác, thành phố thủ đô): QoL 0.54 → 0.29.

## Lỗi mô hình tìm ra khi chạy thật và đã sửa
Lần đầu mức sống quốc gia chỉ cộng tuyến tính: ăn 30% mà QoL chỉ giảm 0.09 và dân không bỏ đi (QoL vẫn >0.30). Test đòi hỏi nạn đói phải gây mất dân và đã bắt được lỗi; sửa bằng hệ số phạt phi tuyến khi thiếu ăn chứ không nới test.

## Hạn chế — nói thẳng
- **Chiều ngược mới chỉ qua QoL → năng suất lao động & tăng dân tỉnh** (xem GĐ6a); thuế/sản xuất riêng của thành phố chưa vào `EconomySim`; dân chi tiết của thành phố chưa đồng bộ hai chiều với `Province.Population`.
- Trọng số 25%, ngưỡng 85%, hệ số 0.4 là tham số tôi đặt, chưa hiệu chỉnh theo trải nghiệm chơi.
- (Đã sửa) `CitySim` nay được lưu trong `SaveGame` phiên bản 5.
- Mỗi lần chỉ nối một thành phố chi tiết; thành phố còn lại ở mức tổng hợp (`City.Population`) và chưa có mô phỏng chi tiết.
- Chưa có thu nhập/giá cả hộ gia đình trong QoL, chưa có nhà ở giá rẻ/nước sạch (như đã ghi ở TDD mục 7.5).
