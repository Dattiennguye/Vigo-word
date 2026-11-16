Dưới đây là tài liệu thiết kế tổng thể phiên bản đầu tiên cho dự án game. Tôi đặt tên dự án tạm thời là VIGO: Empire & Civilization. Tài liệu được viết theo hướng có thể chuyển trực tiếp cho Claude để nghiên cứu kiến trúc và triển khai từng giai đoạn.

# GAME DESIGN DOCUMENT (GDD)
# VIGO: EMPIRE & CIVILIZATION
### Tài liệu định hướng thiết kế và phát triển trò chơi – Version 1.0

---

## I. TỔNG QUAN DỰ ÁN

### 1. Ý tưởng cốt lõi

Phát triển một trò chơi mô phỏng xây dựng quốc gia, quản lý thành phố, phát triển kinh tế, nghiên cứu công nghệ, xây dựng quân đội, ngoại giao và chiến tranh chiến thuật.

Trò chơi kết hợp các yếu tố:
- City Builder – Xây dựng và quản lý thành phố.
- Nation Simulation – Mô phỏng và quản lý quốc gia.
- Grand Strategy – Chiến lược địa chính trị.
- Military Strategy – Quản lý quân đội và chiến tranh.
- Economic Simulation – Mô phỏng kinh tế và thương mại.
- Diplomacy – Ngoại giao và quan hệ quốc tế.

Trò chơi hoàn toàn OFFLINE, chỉ có một người chơi điều khiển quốc gia của mình. Các quốc gia còn lại được điều khiển bởi hệ thống AI độc lập.

### 2. Tầm nhìn sản phẩm

Người chơi bắt đầu từ một quốc gia và phát triển đất nước qua nhiều giai đoạn.

Có thể trở thành:
- Một cường quốc kinh tế.
- Một quốc gia công nghiệp phát triển.
- Một cường quốc quân sự.
- Một trung tâm công nghệ.
- Một quốc gia có ảnh hưởng ngoại giao lớn.
- Một đế chế kiểm soát phần lớn bản đồ.

Không bắt buộc phải dùng chiến tranh để chiến thắng. Người chơi có thể phát triển thông qua kinh tế, ngoại giao, thương mại và công nghệ.

### 3. Định hướng đồ họa

- Thế giới có quy mô rất lớn.
- Địa hình và môi trường 3D.
- Thành phố có kiến trúc hiện đại, đẹp và chi tiết.
- Công trình có thiết kế riêng, không sử dụng một mẫu lặp lại cho toàn bộ thành phố.
- Đường sá, cầu, sông, núi, rừng và biển được thể hiện trực quan.
- Hệ thống ánh sáng, thời tiết và chu kỳ ngày đêm.
- Giao diện hiện đại, dễ sử dụng, mang phong cách game chiến lược cao cấp.

Tham khảo tinh thần thiết kế từ Cities: Skylines, kết hợp chiều sâu chiến lược của các game Grand Strategy.

---

## II. THẾ GIỚI VÀ BẢN ĐỒ

### 1. Quy mô bản đồ

Thế giới là một khu vực rộng lớn thuộc một châu lục giả tưởng, không phải bản đồ thành phố nhỏ.

Trong thế giới có nhiều quốc gia độc lập, mỗi quốc gia sở hữu lãnh thổ, tỉnh thành, thủ đô và hệ thống kinh tế riêng.

Đề xuất cấu hình ban đầu:
- 30 quốc gia.
- Mỗi quốc gia có khoảng 8–20 tỉnh.
- Mỗi tỉnh có khoảng 2–8 thành phố hoặc khu dân cư.
- Mỗi quốc gia có một thủ đô bắt buộc.
- Các quốc gia có diện tích, dân số, tài nguyên và vị trí địa lý khác nhau.

Các thông số trên phải có thể điều chỉnh trong World Generator.

### 2. Hệ thống tạo bản đồ ngẫu nhiên

Mỗi lần bắt đầu trò chơi mới, hệ thống tạo một thế giới khác nhau.

Các yếu tố ngẫu nhiên:
- Hình dạng lục địa.
- Đường bờ biển.
- Ranh giới quốc gia.
- Diện tích quốc gia.
- Số lượng tỉnh.
- Vị trí thủ đô.
- Vị trí các thành phố.
- Hệ thống sông và hồ.
- Dãy núi, cao nguyên và đồng bằng.
- Rừng, sa mạc và vùng khí hậu.
- Mỏ khoáng sản và tài nguyên tự nhiên.

Yêu cầu quan trọng: Ngẫu nhiên phải có logic địa lý.

Ví dụ:
- Thành phố thường xuất hiện gần nguồn nước.
- Khu công nghiệp có thể nằm gần nguyên liệu và tuyến vận tải.
- Thủ đô có thể nằm tại trung tâm chính trị, vị trí giao thông thuận lợi hoặc khu vực chiến lược.
- Thành phố cảng phải tiếp giáp biển hoặc đường thủy phù hợp.
- Khu vực miền núi có đặc điểm xây dựng và giao thông riêng.

Không được tạo thành phố và quốc gia theo dạng lưới vuông cứng nhắc.

### 3. Cấu trúc hành chính bắt buộc

Thế giới phải có cấu trúc phân cấp:

LỤC ĐỊA → QUỐC GIA → TỈNH → THÀNH PHỐ → KHU ĐÔ THỊ → CÔNG TRÌNH.

Mỗi quốc gia phải có:
- Tên quốc gia.
- Quốc kỳ.
- Thủ đô.
- Chính phủ.
- Dân số.
- Ngân sách.
- GDP hoặc chỉ số kinh tế tương đương.
- Quân đội.
- Công nghệ.
- Hệ thống hành chính.
- Danh sách tỉnh và thành phố.

Mỗi tỉnh phải có:
- Tên tỉnh.
- Ranh giới địa lý riêng.
- Dân số.
- Thành phố trực thuộc.
- Tài nguyên.
- Công trình.
- Hệ thống giao thông.
- Sản lượng kinh tế.

Mỗi thành phố phải có:
- Tên thành phố.
- Vị trí trên bản đồ.
- Dân số.
- Khu dân cư.
- Khu thương mại.
- Khu công nghiệp.
- Hạ tầng.
- Các dịch vụ công cộng.

### 4. Thay đổi lãnh thổ

Biên giới quốc gia không cố định.

Khi xảy ra chiến tranh hoặc chuyển giao lãnh thổ:
- Tỉnh có thể bị chiếm đóng.
- Tỉnh có thể được chuyển giao.
- Quốc gia có thể mất một phần lãnh thổ.
- Thành phố có thể thay đổi chủ quyền.
- Thủ đô có thể bị chiếm.
- Quốc gia có thể tan rã hoặc sáp nhập.

Mọi thay đổi phải được cập nhật trên bản đồ chính trị.

---

## III. HỆ THỐNG XÂY DỰNG THÀNH PHỐ

### 1. Quy hoạch đô thị

Người chơi được quyền trực tiếp quy hoạch thành phố.

Các loại công trình:

**Nhà ở**
- Nhà dân thấp tầng.
- Nhà liền kề.
- Biệt thự.
- Chung cư.
- Khu đô thị.
- Tòa nhà cao tầng.

**Thương mại**
- Cửa hàng.
- Siêu thị.
- Trung tâm thương mại.
- Văn phòng.
- Khách sạn.
- Trung tâm tài chính.

**Công nghiệp**
- Nhà máy.
- Kho vận.
- Khu công nghiệp.
- Nhà máy chế biến.
- Nhà máy năng lượng.

**Công cộng**
- Trường học.
- Đại học.
- Bệnh viện.
- Công viên.
- Sân vận động.
- Trung tâm văn hóa.

**Hành chính**
- Tòa thị chính.
- Cơ quan chính phủ.
- Tòa nhà quốc hội.
- Trung tâm hành chính tỉnh.

**Quân sự**
- Doanh trại.
- Học viện quân sự.
- Căn cứ không quân.
- Căn cứ hải quân.
- Trung tâm chỉ huy.
- Nhà máy quốc phòng.

### 2. Giao thông

Hệ thống giao thông phải có chiều sâu.

- Đường dân cư.
- Đường đô thị.
- Đại lộ.
- Đường cao tốc.
- Cầu.
- Hầm.
- Đường sắt.
- Nhà ga.
- Cảng biển.
- Sân bay.

Giao thông ảnh hưởng trực tiếp đến:
- Thời gian di chuyển.
- Vận tải hàng hóa.
- Khả năng phát triển đô thị.
- Hoạt động công nghiệp.
- Hậu cần quân sự.

### 3. Mô phỏng dân cư

Người dân có nhu cầu:
- Nhà ở.
- Việc làm.
- Thực phẩm.
- Y tế.
- Giáo dục.
- Giao thông.
- An ninh.
- Giải trí.

Các chỉ số:
- Tỷ lệ thất nghiệp.
- Tăng trưởng dân số.
- Thu nhập.
- Chất lượng cuộc sống.
- Mức độ hài lòng.
- Tỷ lệ tội phạm.
- Mức độ ô nhiễm.

Thành phố phát triển tốt sẽ thu hút thêm dân cư và tạo ra nguồn thu lớn hơn.

---

## IV. HỆ THỐNG KINH TẾ QUỐC GIA

### 1. Ngân sách

Người chơi quản lý:
- Thuế dân cư.
- Thuế doanh nghiệp.
- Thuế thương mại.
- Chi tiêu chính phủ.
- Ngân sách quốc phòng.
- Ngân sách giáo dục.
- Ngân sách y tế.
- Đầu tư hạ tầng.
- Nghiên cứu khoa học.

### 2. Tài nguyên

Các nhóm tài nguyên:
- Lương thực.
- Nước.
- Gỗ.
- Than.
- Dầu mỏ.
- Khí đốt.
- Sắt.
- Đồng.
- Nhôm.
- Kim loại quý.
- Vật liệu công nghiệp.

Tài nguyên phải phân bố không đồng đều giữa các quốc gia.

### 3. Sản xuất

Mỗi quốc gia có thể phát triển:
- Nông nghiệp.
- Khai khoáng.
- Năng lượng.
- Công nghiệp nặng.
- Công nghiệp nhẹ.
- Sản xuất phương tiện.
- Công nghiệp quốc phòng.
- Công nghệ cao.

Sản lượng phụ thuộc vào nhân lực, tài nguyên, công nghệ, cơ sở hạ tầng và vốn đầu tư.

---

## V. HỆ THỐNG QUÂN ĐỘI

### 1. Tuyển quân

Có hai nhóm nhân lực chính:

**Lính nghĩa vụ**
- Tuyển theo chính sách nghĩa vụ quân sự.
- Phục vụ theo thời hạn.
- Chi phí nhân sự thấp hơn.
- Cần cân đối với lực lượng lao động dân sự.

**Lính chuyên nghiệp**
- Được trả lương thường xuyên.
- Đào tạo chuyên sâu.
- Có kinh nghiệm chiến đấu.
- Có thể phục vụ lâu dài.

Ngoài ra có:
- Sĩ quan.
- Chỉ huy.
- Kỹ thuật viên.
- Nhân viên hậu cần.
- Quân nhân chuyên ngành.

### 2. Quân chủng

**Lục quân**
- Bộ binh.
- Cơ giới.
- Thiết giáp.
- Pháo binh.
- Công binh.

**Không quân**
- Tiêm kích.
- Máy bay vận tải.
- Trực thăng.
- Máy bay trinh sát.

**Hải quân**
- Tàu tuần tra.
- Tàu hộ vệ.
- Tàu khu trục.
- Tàu ngầm.
- Tàu vận tải.

**Phòng không**
- Radar.
- Hệ thống phòng thủ.
- Tên lửa phòng không.

### 3. Đào tạo và trang bị

Quân đội có các chỉ số:
- Quân số.
- Kinh nghiệm.
- Tinh thần.
- Huấn luyện.
- Trang bị.
- Khả năng hậu cần.
- Khả năng tác chiến.

Người chơi xây dựng học viện, trường đào tạo, thao trường và cơ sở bảo dưỡng.

### 4. Công nghiệp quốc phòng

Cho phép xây dựng nhà máy sản xuất:
- Súng bộ binh.
- Xe quân sự.
- Xe tăng.
- Pháo.
- Máy bay.
- Tàu chiến.
- Radar.
- Thiết bị liên lạc.

Trang bị được sản xuất phải có chi phí, thời gian và yêu cầu công nghệ.

---

## VI. HỆ THỐNG NGHIÊN CỨU CÔNG NGHỆ

Cây công nghệ gồm 5 nhánh:

1. Công nghệ dân sự.
2. Công nghệ công nghiệp.
3. Công nghệ quân sự.
4. Hàng không và hải quân.
5. Điện tử và tự động hóa.

Nghiên cứu cần:
- Ngân sách.
- Nhân lực chuyên môn.
- Trung tâm nghiên cứu.
- Thời gian.
- Nguyên liệu.

Công nghệ mới mở khóa công trình, thiết bị, phương tiện và phương thức sản xuất mới.

Không được để tất cả công nghệ mở khóa ngay từ đầu.

---

## VII. HỆ THỐNG AI QUỐC GIA

Đây là một trong những hệ thống quan trọng nhất.

Mỗi quốc gia AI phải có khả năng tự vận hành mà không cần người chơi can thiệp.

### 1. Tính cách AI

Có các kiểu quốc gia:

- Quốc gia thương mại.
- Quốc gia công nghiệp.
- Quốc gia quân sự.
- Quốc gia công nghệ.
- Quốc gia trung lập.
- Quốc gia bành trướng.
- Quốc gia cân bằng.

### 2. Khả năng của AI

AI phải tự:
- Xây dựng thành phố.
- Phát triển dân số.
- Thu thuế.
- Đầu tư công nghiệp.
- Quản lý ngân sách.
- Nghiên cứu công nghệ.
- Tuyển quân.
- Xây dựng quân đội.
- Mua bán tài nguyên.
- Thiết lập quan hệ ngoại giao.
- Phản ứng trước chiến tranh.
- Đánh giá sức mạnh đối thủ.

### 3. AI thích nghi

AI không được hoạt động theo kịch bản cố định.

Ví dụ:
- Nếu thiếu lương thực, AI tìm cách nhập khẩu hoặc mở rộng nông nghiệp.
- Nếu có nguy cơ chiến tranh, AI tăng đầu tư quốc phòng.
- Nếu nền kinh tế suy yếu, AI cắt giảm chi tiêu hoặc điều chỉnh thuế.
- Nếu đối thủ quá mạnh, AI có thể tìm kiếm đồng minh.
- Nếu có lợi thế kinh tế, AI có thể mở rộng ảnh hưởng thông qua thương mại.

---

## VIII. NGOẠI GIAO VÀ QUAN HỆ QUỐC TẾ

Người chơi có thể giao tiếp và đàm phán với các quốc gia AI.

### 1. Quan hệ ngoại giao

- Hòa bình.
- Trung lập.
- Đối tác thương mại.
- Đồng minh.
- Liên minh quân sự.
- Đối địch.

### 2. Hiệp ước

- Hiệp ước không xâm lược.
- Hiệp ước thương mại.
- Hiệp ước phòng thủ chung.
- Hiệp ước hỗ trợ quân sự.
- Hiệp ước chuyển giao công nghệ.
- Hiệp ước đầu tư.
- Hiệp ước hòa bình.

### 3. Giao thương

Các quốc gia có thể:
- Mua tài nguyên.
- Bán tài nguyên.
- Nhập khẩu lương thực.
- Xuất khẩu máy móc.
- Mua phương tiện.
- Trao đổi công nghệ.
- Ký hợp đồng dài hạn.

Giá cả được xác định bởi cung cầu, nguồn cung, nhu cầu và tình hình thế giới.

### 4. Quan hệ AI

AI phải có khả năng đánh giá người chơi dựa trên:
- Sức mạnh kinh tế.
- Sức mạnh quân sự.
- Quan hệ trước đó.
- Mức độ đáng tin cậy.
- Các hiệp ước đang tồn tại.
- Lợi ích chiến lược.

---

## IX. CHIẾN TRANH VÀ TRANH GIÀNH LÃNH THỔ

### 1. Chiến tranh chiến lược

Người chơi có thể:
- Tuyên chiến.
- Điều động quân đội.
- Bố trí lực lượng.
- Bảo vệ biên giới.
- Chiếm đóng tỉnh.
- Phong tỏa tuyến vận tải.
- Hỗ trợ đồng minh.
- Đàm phán đình chiến.

### 2. Chiến tranh chiến thuật

Định hướng có hai lớp:

- Bản đồ chiến lược để quản lý quân đội trên toàn quốc gia.
- Chiến trường 3D để quan sát và điều khiển đơn vị chiến đấu.

### 3. Kết quả chiến tranh

Phụ thuộc vào:
- Quân số.
- Trang bị.
- Công nghệ.
- Kinh nghiệm.
- Địa hình.
- Hậu cần.
- Tinh thần.
- Khả năng chỉ huy.

Không được chỉ tính kết quả bằng số lượng quân đội.

### 4. Hậu quả chiến tranh

Chiến tranh gây:
- Thiệt hại nhân lực.
- Suy giảm kinh tế.
- Hư hỏng cơ sở hạ tầng.
- Khủng hoảng ngân sách.
- Biến động dân cư.
- Thay đổi biên giới.
- Bất ổn chính trị.

---

## X. GIAO DIỆN VÀ CAMERA

Thiết kế ba cấp độ quan sát:

**Cấp 1 – Bản đồ khu vực thế giới**
- Hiển thị toàn bộ quốc gia.
- Biên giới.
- Thủ đô.
- Thành phố lớn.
- Quan hệ ngoại giao.
- Tình hình chiến tranh.

**Cấp 2 – Bản đồ tỉnh**
- Hiển thị ranh giới tỉnh.
- Tài nguyên.
- Dân số.
- Thành phố.
- Công trình.
- Đường giao thông.

**Cấp 3 – Thành phố 3D**
- Xem chi tiết công trình.
- Xây dựng nhà cửa.
- Quy hoạch đường sá.
- Quản lý khu dân cư.
- Quan sát phương tiện và hoạt động đô thị.

Camera hỗ trợ:
- Phóng to.
- Thu nhỏ.
- Xoay.
- Di chuyển tự do.
- Chuyển đổi các lớp bản đồ.

---

## XI. KIẾN TRÚC KỸ THUẬT

### 1. Định hướng công nghệ

Đề xuất nghiên cứu Unity hoặc Unreal Engine.

Ưu tiên lựa chọn dựa trên:
- Khả năng xử lý bản đồ rất lớn.
- Hệ thống địa hình 3D.
- Xây dựng thành phố.
- Mô phỏng hàng nghìn công trình.
- Hệ thống AI quốc gia.
- Chiến tranh thời gian thực.
- Tối ưu hóa CPU và GPU.

Không bắt buộc phải sử dụng một engine trước khi đánh giá thử nghiệm kỹ thuật.

### 2. Cấu trúc hệ thống

Chia thành các module độc lập:

- World Generator.
- Terrain System.
- Country System.
- Province System.
- City Builder.
- Population Simulation.
- Economy System.
- Resource System.
- Military System.
- Technology System.
- Diplomacy System.
- AI Country System.
- War Simulation.
- Save/Load System.
- UI/UX System.

### 3. Dữ liệu

Các đối tượng phải có ID riêng và quan hệ dữ liệu rõ ràng.

Ví dụ:

World ID → Country ID → Province ID → City ID → Building ID.

Mỗi tỉnh phải biết quốc gia sở hữu. Mỗi thành phố phải biết tỉnh trực thuộc. Khi thay đổi chủ quyền, dữ liệu phải được cập nhật đồng bộ.

### 4. Lưu trữ

Game phải hỗ trợ:
- Lưu game.
- Tải game.
- Nhiều vị trí lưu.
- Tự động lưu.
- Lưu trạng thái AI.
- Lưu quan hệ ngoại giao.
- Lưu chiến tranh và lịch sử.
- Lưu toàn bộ thay đổi trên bản đồ.

Game phải có khả năng tiếp tục đúng trạng thái trước khi thoát.

---

## XII. LỘ TRÌNH PHÁT TRIỂN

Không triển khai tất cả hệ thống cùng lúc.

### Giai đoạn 1 – World Generator

- Tạo bản đồ rộng lớn.
- Sinh địa hình.
- Sinh quốc gia.
- Sinh biên giới.
- Sinh tỉnh.
- Sinh thành phố.
- Xác định thủ đô.
- Tạo dữ liệu tài nguyên.

Kết quả: Một thế giới hoàn chỉnh có thể khám phá.

### Giai đoạn 2 – City Builder

- Camera 3D.
- Xây dựng đường.
- Xây nhà.
- Xây công trình.
- Phân khu đô thị.
- Quản lý dân cư.

Kết quả: Người chơi xây dựng được thành phố.

### Giai đoạn 3 – Economy Simulation

- Thuế.
- Ngân sách.
- Sản xuất.
- Tài nguyên.
- Thương mại.
- Dân số.
- Việc làm.

Kết quả: Thành phố và quốc gia có nền kinh tế vận hành.

### Giai đoạn 4 – AI Countries

- AI phát triển quốc gia.
- AI xây dựng thành phố.
- AI quản lý ngân sách.
- AI sản xuất.
- AI nghiên cứu công nghệ.
- AI cạnh tranh với người chơi.

Kết quả: Các quốc gia độc lập cùng hoạt động trong một thế giới.

### Giai đoạn 5 – Military System

- Tuyển quân.
- Doanh trại.
- Đào tạo.
- Quân chủng.
- Trang bị.
- Công nghiệp quốc phòng.
- Điều động quân đội.

### Giai đoạn 6 – Diplomacy

- Quan hệ quốc tế.
- Hiệp ước.
- Thương mại.
- Đồng minh.
- Đàm phán.
- Chính sách đối ngoại.

### Giai đoạn 7 – Warfare

- Tuyên chiến.
- Điều quân.
- Chiến trường.
- Chiếm đóng.
- Thay đổi biên giới.
- Hòa bình.

### Giai đoạn 8 – Hoàn thiện

- Giao diện.
- Âm thanh.
- Hiệu ứng.
- Tối ưu hiệu suất.
- Lưu và tải game.
- Kiểm thử AI.
- Kiểm thử thế giới lớn.

---

## XIII. NGUYÊN TẮC PHÁT TRIỂN BẮT BUỘC

1. Không tạo một bản demo chỉ có giao diện đẹp nhưng không có hệ thống hoạt động thực tế.
2. Các module phải kết nối với nhau bằng dữ liệu thật.
3. Không sử dụng dữ liệu giả để mô phỏng các chức năng đã tuyên bố hoàn thiện.
4. Mọi quốc gia đều phải có cấu trúc hành chính thống nhất.
5. Bản đồ phải có khả năng mở rộng.
6. AI phải có hành vi độc lập.
7. Chiến tranh phải tác động đến kinh tế, dân số và lãnh thổ.
8. Ngoại giao phải có tác động thực tế đến quan hệ giữa các quốc gia.
9. Người chơi phải có nhiều con đường phát triển.
10. Hệ thống phải được thiết kế để dễ mở rộng tính năng trong tương lai.

Đặc biệt, cần ưu tiên hiệu suất ngay từ kiến trúc ban đầu. Không mô phỏng từng người dân hoặc từng vật thể ở mức chi tiết tối đa trên toàn bộ bản đồ. Sử dụng mô phỏng phân cấp theo khoảng cách camera và mức độ quan trọng.

---

## XIV. TIÊU CHUẨN SẢN PHẨM CUỐI CÙNG

Sản phẩm hướng tới một game mô phỏng quốc gia có chiều sâu, trong đó:

- Một thế giới rộng lớn với nhiều quốc gia.
- Mỗi quốc gia có thủ đô, tỉnh và thành phố.
- Người chơi tự xây dựng và quy hoạch thành phố.
- Kinh tế và dân cư vận hành liên tục.
- Quân đội có thể tuyển dụng, đào tạo và nâng cấp.
- Công nghệ mở khóa các năng lực mới.
- AI tự xây dựng và phát triển quốc gia.
- Có thương mại và ngoại giao.
- Có chiến tranh và tranh giành lãnh thổ.
- Bản đồ có thể thay đổi theo lịch sử thế giới.
- Chơi hoàn toàn offline.
- Có hệ thống lưu và tiếp tục trò chơi.

### ĐỊNH HƯỚNG CUỐI CÙNG

Đây không đơn thuần là một game xây dựng thành phố có thêm xe tăng và quân đội.

Đây là một hệ thống mô phỏng thế giới, trong đó thành phố tạo ra nền kinh tế, nền kinh tế nuôi dưỡng quốc gia, công nghệ tạo ra lợi thế, quân đội bảo vệ và mở rộng lãnh thổ, còn ngoại giao quyết định quan hệ giữa các quốc gia.

Mục tiêu là tạo ra một thế giới có khả năng tự vận hành và phát triển, nơi mỗi lần bắt đầu trò chơi đều có thể hình thành một lịch sử hoàn toàn khác nhau.

**END OF GDD – VERSION 1.0**

Đề xuất bước tiếp theo: Chuyển tài liệu này thành bộ đặc tả kỹ thuật (Technical Design Document – TDD), trong đó xác định cách Claude xây dựng World Generator, hệ thống bản đồ lớn, AI quốc gia và kiến trúc mô phỏng trước khi bắt đầu lập trình.
