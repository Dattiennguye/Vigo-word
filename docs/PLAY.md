# Chơi VIGO (bản tự dựng, không dùng Unity)

Giao diện chơi được viết bằng HTML5 Canvas (phong cách isometric hoạt hình, màu tươi) chạy trên trình duyệt, nói chuyện với lõi C# (`Vigo.Core` → `GameSession`) qua một máy chủ cục bộ nhỏ `src/Vigo.Game` (ASP.NET Core minimal API). Mọi luật chơi vẫn nằm ở `Vigo.Core`; giao diện chỉ hiển thị và gửi lệnh.

## Chạy
- Windows: cài .NET 8 SDK, bấm đúp `Choi-VIGO.bat` (hoặc `dotnet run --project src/Vigo.Game -c Release`), trình duyệt mở `http://localhost:5077`.
- Linux/macOS: `./choi-vigo.sh`.
- Điện thoại (tạm thời): chạy máy chủ trên PC với `VIGO_URL=http://0.0.0.0:5077` rồi mở `http://<IP-của-PC>:5077` trên trình duyệt điện thoại cùng Wi-Fi. Giao diện có bố cục cho màn hình hẹp và cảm ứng (kéo, chụm hai ngón).

## Trong game
- **Màn hình đầu**: tạo thế giới (mã seed + kích thước), hoặc "Chơi tiếp" một bản đã lưu. Sau đó chọn quốc gia của bạn.
- **Bản đồ thế giới**: kéo để di chuyển, lăn chuột/chụm để phóng to, bấm tỉnh/thành phố để xem. Đồng hồ ▶ ⏩ ⏭ (phím 1/2/3), Space để tạm dừng.
- **Quốc gia**: thuế và 4 khoản ngân sách (giáo dục, y tế, đầu tư, quốc phòng), các thanh đời sống (lương thực, nhiên liệu, hàng hóa, giáo dục, y tế).
- **Ngoại giao**: quan hệ với từng nước, tuyên chiến/xin hòa/liên minh/hiệp ước/thương mại; hộp thư đề nghị từ AI (chấp nhận/từ chối).
- **Tin tức**: chiến tranh, chiếm tỉnh, hiệp ước…
- **Thành phố**: mở thành phố của bạn (thị trấn mẫu hoặc đất trống); công cụ đường (4 loại), phân khu nhà ở/thương mại/công nghiệp, trường học/bệnh viện/công an/công viên, phá bỏ. Chỉ số: QoL, việc làm, y tế, giáo dục, an ninh, đi lại, môi trường, giải trí, nhu cầu xây thêm.
- **💾**: lưu, nạp, quay lại mốc tự động (mỗi quý), tự lưu mỗi 4 năm vào `tudongluu`. Mất hết tỉnh = kết thúc, quay lại mốc cũ được.

## API (cho ai muốn làm giao diện khác)
`/api/new /map /world /player /state /step /tax /budget /diplo /offer /save /load /saves /rewind /history /city/open /city /city/cmd` — xem `src/Vigo.Game/Api.cs`.

## Hạn chế — nói thẳng
- Cần máy chạy .NET 8 (máy chủ cục bộ). **Chưa có bản Android chạy độc lập**: môi trường dựng ở đây không có công cụ biên dịch C# sang WebAssembly/Android, nên điện thoại hiện phải kết nối tới PC. Hướng tiếp theo cho Android độc lập: đóng gói web + chuyển lõi sang WebAssembly, hoặc dùng Unity/MAUI trên máy có công cụ đó.
- Hình ảnh là Canvas 2D vẽ bằng mã (chưa có sprite/animation tay vẽ); bản đồ thế giới vẽ từng ô nên hơi lốm đốm.
- Thành phố chỉ dùng được cho thành phố của bạn; thành phố của nước khác chỉ ở mức tổng hợp.
- Chưa có âm thanh, hướng dẫn chi tiết, hay bản đồ nhiệt (tài nguyên/kinh tế) trên bản đồ.
- Giao diện chưa kiểm thử trên điện thoại thật; kiểm thử tự động mới là chụp ảnh bằng Playwright (`tools/smoke_ui.py`).
