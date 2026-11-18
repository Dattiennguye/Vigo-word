# Hệ thống lưu / nạp / quay lại (SaveGame)

Mã: `src/Vigo.Core/SaveGame.cs` (`SaveGame`, `RewindBuffer`). Test: 3 test "Lưu/nạp" và "Quay lại" trong `EconomyTests.cs` (tổng 37 test đạt).

## Thiết kế
- Thế giới sinh **tất định theo seed**, nên bản lưu KHÔNG chứa địa hình. Nó chứa: cấu hình, `GenesisHash` (hash thế giới ngay sau khi sinh), phần thế giới đã đổi (chủ tỉnh, dân, thủ đô, danh sách tỉnh, cờ bị diệt) và toàn bộ trạng thái kinh tế (tồn kho, giá, kho bạc, thuế, công suất, mỏ còn lại, cấm vận, sổ cái).
- Nạp = sinh lại thế giới từ seed → so `GenesisHash` → phủ phần đã đổi → dựng lại dữ liệu dẫn xuất (láng giềng, cảng).
- Định dạng: gzip, có magic `VIGO`, số phiên bản, tổng kiểm FNV-64. Bản hỏng/cắt cụt/rác/rỗng bị từ chối bằng `SaveException` (không treo, không crash).
- `SaveGame.Save` ghi file tạm rồi đổi tên: mất điện giữa chừng không làm hỏng bản cũ.
- `RewindBuffer`: vòng bản lưu trong bộ nhớ (ví dụ mỗi năm game một mốc). `Rewind(tuần)` về mốc gần nhất không muộn hơn, bỏ các mốc muộn (nhánh lịch sử sai) → đáp ứng "chọn sai thì làm lại".

## Đo được (20k ô, 30 nước)
Bản lưu ≈ 39 KB, lưu ≈ 7 ms, nạp ≈ 250 ms (chủ yếu là sinh lại thế giới). Nạp rồi chạy tiếp 100 tuần cho trạng thái **giống hệt** chạy liền (kể cả sau chiến tranh, đổi thuế, cấm vận).

## Hạn chế
- Chưa lưu `CitySim` (thành phố chi tiết); khi có lớp Unity phải thêm phần này.
- Bản lưu phụ thuộc bộ sinh thế giới: đổi thuật toán sinh làm hash gốc lệch → bản cũ bị từ chối (chưa có bộ chuyển đổi phiên bản).
- Tất định xuyên nền tảng (Windows ↔ Android) chưa kiểm; nếu float/double lệch, `GenesisHash` sẽ từ chối thay vì nạp sai âm thầm.
- Mốc bộ nhớ cho 40 năm game ≈ 40 × 40 KB: nhỏ, nhưng sẽ lớn hơn khi thêm quân đội, AI, thành phố.
