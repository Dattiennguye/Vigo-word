# Giai đoạn 4a – AI quốc gia (điều hành ngân sách)

Mã: `src/Vigo.Core/AI/CountryAI.cs`. Test: `src/Vigo.Tests/AITests.cs` (5 test; tổng 42 test đạt).
Chẩn đoán: `dotnet run --project src/Vigo.Cli -- --aidiag 1` (so từng chính sách) và `--aishock 1`.

## AI làm gì
Mỗi 4 tuần, mỗi quốc gia không phải của người chơi (`SetHuman`) được AI đặt ngân sách và ưu tiên đầu tư qua đúng API người chơi dùng (`SetBudget`, `SetInvestWeights`, `SetTaxRate`). Tất định, không dùng RNG.
- **Tính cách** quyết định cơ cấu chi: Quân sự/Bành trướng chi quốc phòng cao, Công nghệ chi giáo dục cao, Công nghiệp chi đầu tư cao, Thương mại chi quốc phòng thấp.
- **Thắt lưng buộc bụng**: nợ > 6 tuần thuế tiềm năng → cắt quốc phòng, giáo dục, đầu tư; thiếu ăn (<95%) → cắt quốc phòng mạnh, giữ y tế.
- **Đầu tư thông minh**: dồn vào ngành có giá cao (khan hiếm); thiếu ăn thì ưu tiên nông nghiệp.
- Thuế giữ 25%.

## Kết quả thử nghiệm — nói thẳng
Tôi đã giả định AI sẽ "làm kinh tế tốt hơn" và phép đo bác bỏ giả định đó:
- **Tăng thuế khi mắc nợ làm tệ hơn** (nợ/người 46 → 112): trong mô hình, thuế >25% giảm sản lượng và chi tiêu tính theo tỉ lệ thuế nên chi tăng theo. Đã bỏ chính sách này.
- Sau khi bỏ, AI **không cải thiện** nợ/đời sống so với chính sách cố định (đời sống 0.91 → 0.87, 3 seed × 30 năm); chi quốc phòng theo tính cách có giá phải trả về đời sống. Cú sốc mất 60% đất canh tác cũng không phân biệt được hai bên vì cơ chế vay cứu đói đã giữ mọi nước đủ ăn.
- Vì vậy test KHÔNG khẳng định AI tối ưu. Test kiểm: tất định; lệnh hợp lệ; không đụng nước người chơi; tính cách tạo chính sách khác nhau; phản ứng đúng khi thiếu ăn; không làm mất ổn định (không thêm nước đói, đời sống không tụt quá 0.08).
- Giá trị hiện tại của AI là **đa dạng hành vi và phản ứng cú sốc**, chưa phải tối ưu hóa. Muốn AI thật sự "giỏi" cần các đòn bẩy có ý nghĩa hơn (quân đội, ngoại giao, thương mại chủ động) — thuộc 4b trở đi.

## Chưa làm
Ngoại giao (liên minh, hiệp ước, cấm vận chủ động), quân đội/chiến tranh/tuyên chiến, mở rộng lãnh thổ, AI tự đặt thuế theo chu kỳ, AI nhìn thấy kinh tế của nước khác (hiện chỉ đọc chỉ số nước mình).
