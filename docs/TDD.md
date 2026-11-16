# TECHNICAL DESIGN DOCUMENT (TDD)
# VIGO: EMPIRE & CIVILIZATION
### Đặc tả kỹ thuật – Version 1.0 (dựa trên GDD v1.0)

> Tài liệu này biến GDD thành các quyết định kỹ thuật cụ thể: engine, kiến trúc mô phỏng, mô hình dữ liệu, thuật toán World Generator, AI quốc gia, kinh tế, chiến tranh, lưu trữ, hiệu năng và tiêu chí nghiệm thu từng giai đoạn.
> Các mục đánh dấu **[QUYẾT ĐỊNH MỞ]** cần chủ dự án xác nhận trước khi code.

---

## 1. NGUYÊN TẮC KIẾN TRÚC

1. **Tách mô phỏng khỏi hiển thị.** Toàn bộ logic game (thế giới, kinh tế, AI, chiến tranh) chạy trong một lõi `Simulation Core` thuần C#, không phụ thuộc UnityEngine. Lớp hiển thị chỉ đọc trạng thái và gửi lệnh (command).
2. **Dữ liệu là nguồn sự thật duy nhất.** Mọi module đọc/ghi qua `WorldState`. Không module nào giữ bản sao dữ liệu riêng (đáp ứng nguyên tắc GDD XIII.2–3: không dữ liệu giả).
3. **Mô phỏng phân cấp (LOD mô phỏng).** Mô phỏng ở mức *tỉnh* cho toàn thế giới; chỉ mô phỏng chi tiết ở mức *thành phố/khu phố* cho khu vực người chơi đang xem hoặc sở hữu. Không mô phỏng từng người dân trên toàn bản đồ.
4. **Tất định (deterministic).** Cùng `seed` + cùng chuỗi lệnh → cùng kết quả. Cho phép debug, replay, kiểm thử AI hàng loạt và so sánh bản lưu.
5. **Command pattern.** Mọi hành động người chơi và AI đều là `Command` đi qua một hàng đợi duy nhất → AI và người chơi dùng chung luật, dễ lưu lịch sử.
6. **Module độc lập, giao tiếp bằng sự kiện + dữ liệu.** Mỗi hệ thống (Economy, Military…) đăng ký vào vòng lặp tick, không gọi chéo trực tiếp.

---

## 2. CÔNG NGHỆ

### 2.1 Đề xuất engine: **Unity (LTS) + C#**

| Tiêu chí (GDD XI.1) | Unity | Unreal |
|---|---|---|
| Ngôn ngữ/độ nhanh lặp | C#, nhanh | C++/Blueprint, build lâu |
| Mô phỏng lớn, nhiều thực thể | DOTS/ECS, Burst, Jobs | Mass Entity (mới hơn) |
| Terrain/streaming bản đồ lớn | Terrain + mesh tự sinh, đủ dùng | Mạnh hơn nhưng nặng |
| Phù hợp lập trình solo/nhỏ | Cao | Thấp hơn |
| Tải tài nguyên/PC phổ thông | Nhẹ hơn | Nặng hơn |

**Khuyến nghị:** Unity, vì dự án là mô phỏng nặng dữ liệu hơn là đồ họa điện ảnh, và nhóm nhỏ cần vòng lặp phát triển nhanh. **[QUYẾT ĐỊNH MỞ #1]** – xác nhận engine sau khi chạy **Technical Spike** (mục 13).

### 2.2 Thành phần chính
- **Simulation Core**: .NET Standard 2.1 assembly riêng, kiểm thử bằng NUnit ngoài Unity.
- **Collections/Jobs/Burst**: cho World Generator, pathfinding, mô phỏng kinh tế hàng loạt.
- **Lưu trữ**: định dạng nhị phân có phiên bản (MessagePack hoặc tự định nghĩa) + nén LZ4; tùy chọn SQLite cho lịch sử/sự kiện.
- **Render**: URP; UI bằng UI Toolkit.
- **CI**: chạy unit test + test mô phỏng tất định (headless) mỗi lần push.

---

## 3. KIẾN TRÚC TỔNG THỂ

```
┌────────────────────────────── Presentation (Unity) ──────────────────────────────┐
│  Camera/LOD · Terrain render · City 3D · Map layers · UI Toolkit · Audio · VFX   │
└───────────▲──────────────── đọc snapshot ────────────────┬──────── Commands ─────┘
            │                                               ▼
┌───────────┴────────────── Simulation Core (C# thuần) ────────────────────────────┐
│  WorldState (ECS/Tables)   ◄──  Command Queue  ◄── Player / AI                   │
│  Tick Scheduler ─► Economy · Population · Resource · Tech · Military ·           │
│                    Diplomacy · AI Country · War · Events                         │
│  Event Bus · RNG (seeded streams) · Save/Load · History Log                      │
└───────────────────────────────────────────────────────────────────────────────────┘
            ▲
┌───────────┴─────────── World Generator (offline pipeline, chạy khi New Game) ────┐
│  Terrain → Climate → Hydrology → Biome → Resources → Countries → Provinces →     │
│  Cities → Capitals → Road/Port seeds → Validate                                   │
└───────────────────────────────────────────────────────────────────────────────────┘
```

### 3.1 Cấu trúc assembly / thư mục
```
/Assets/Vigo/
  Core/            (Simulation Core – không dùng UnityEngine)
    Data/          WorldState, entity tables, ID types
    Commands/      Command definitions + validators
    Systems/       Economy, Population, Resource, Tech, Military, Diplomacy, War
    AI/            Utility AI, personalities, planners
    Generation/    World Generator stages
    Save/          Serializer, migration
  Presentation/    Rendering, camera, UI, audio
  Tests/           EditMode (Core), PlayMode (smoke)
/docs/             GDD.md, TDD.md
```

---

## 4. MÔ HÌNH DỮ LIỆU

### 4.1 ID
Mọi thực thể có ID số nguyên 32-bit kèm kiểu mạnh (`CountryId`, `ProvinceId`, `CityId`, `BuildingId`, `UnitId`, `TreatyId`…). ID không bao giờ tái sử dụng trong một thế giới.

Phân cấp (GDD XI.3):
`World → Continent → Country → Province → City → District → Building`

### 4.2 Lưu trữ trong bộ nhớ
Dạng **bảng cột (struct-of-arrays)** thay vì cây đối tượng:
- `Countries[]`, `Provinces[]`, `Cities[]`, `Buildings[]`, `Armies[]`, `Treaties[]`…
- Quan hệ bằng khóa ngoại: `Province.OwnerCountryId`, `City.ProvinceId`, `Building.CityId`.
- Chỉ mục phụ: danh sách tỉnh theo quốc gia, thành phố theo tỉnh (cập nhật trong **một hàm duy nhất** khi đổi chủ quyền).

### 4.3 Thực thể chính (rút gọn)

**Country**: `Id, Name, FlagSeed, CapitalCityId, Government, Treasury, DebtTotal, TaxPolicy, BudgetAllocation, TechState, Personality, DiplomacyRelations[], AtWarWith[]`

**Province**: `Id, Name, OwnerCountryId, OriginalOwnerId, CellIds[] (vùng Voronoi), Population, ResourceDeposits[], Infrastructure, Output (GDP tỉnh), Stability, OccupationState`

**City**: `Id, Name, ProvinceId, Position, Population, Districts[], Services, Happiness, Pollution, Crime, IsPort, IsCapital`

**Building**: `Id, Type, CityId, Level, Position, Footprint, Workers, Efficiency, Condition`

**Army/Unit**: `Id, CountryId, Composition[], Experience, Morale, Supply, Position (ProvinceId hoặc đường đi), Orders`

### 4.4 Bản đồ vật lý
- **Lưới ô (cell graph) bất quy tắc** dựa trên Voronoi/Delaunay (~200.000–400.000 ô cho lục địa), mỗi ô có: độ cao, nhiệt độ, độ ẩm, biome, loại nước, tài nguyên, `ProvinceId`.
- Cạnh ô → đồ thị lân cận dùng cho biên giới, sông, vận tải, di chuyển quân.
- Không dùng lưới vuông cứng (GDD II.2). Terrain 3D được sinh mesh từ heightfield nội suy trên cell graph, chia **chunk** để stream theo camera.

---

## 5. VÒNG LẶP MÔ PHỎNG

### 5.1 Thời gian
- **Tick cơ sở = 1 ngày game.** Tốc độ: tạm dừng, 1×, 2×, 5×, 20×.
- Các hệ thống chạy theo chu kỳ khác nhau để tiết kiệm CPU:

| Hệ thống | Chu kỳ | Mức chi tiết |
|---|---|---|
| Di chuyển quân, chiến sự | mỗi ngày | đơn vị / tỉnh |
| Sản xuất, tiêu thụ tài nguyên | mỗi ngày (gộp theo tỉnh) | tỉnh; thành phố khi được xem |
| Dân số, việc làm, hài lòng | mỗi tuần | thành phố |
| Thuế, ngân sách | mỗi tháng | quốc gia |
| Nghiên cứu công nghệ | mỗi tuần | quốc gia |
| Quyết định AI chiến lược | mỗi 5–10 ngày (xen kẽ giữa các AI) | quốc gia |
| Giá thị trường | mỗi tuần | thế giới |

### 5.2 Phân chia LOD mô phỏng
| Mức | Áp dụng | Nội dung |
|---|---|---|
| L0 – Tổng hợp | Thành phố AI xa người chơi | Chỉ số gộp (dân số, sản lượng, nhu cầu) cập nhật công thức |
| L1 – Thành phố | Thành phố người chơi, thành phố đang xem | Mô phỏng khu dân cư, tòa nhà, dịch vụ, giao thông tổng hợp |
| L2 – Chi tiết | Khu vực camera đang zoom | Phương tiện/người đi bộ mang tính hiển thị (agent nhẹ, không ảnh hưởng luật) |

Chuyển L0 ↔ L1 phải **bảo toàn số liệu**: khi thành phố AI được người chơi xem, trạng thái chi tiết được *sinh ra từ* chỉ số gộp theo công thức xác định, và khi rời đi được *gộp lại*. Tránh sai lệch cộng dồn bằng cách coi chỉ số gộp là nguồn sự thật.

### 5.3 Thứ tự trong một tick
1. Thu thập `Command` (người chơi + AI) → kiểm tra hợp lệ → áp dụng.
2. Hệ thống theo thứ tự cố định: Resource → Production → Population → Economy → Technology → Military → Diplomacy → War → Events.
3. Phát sự kiện, cập nhật chỉ mục, ghi lịch sử.
4. Đẩy snapshot cho Presentation.

Mô phỏng chạy trên thread riêng/Jobs; Presentation chỉ đọc snapshot bất biến (double-buffer).

---

## 6. WORLD GENERATOR (Giai đoạn 1)

Đầu vào: `WorldConfig { seed, countryCount=30, provincesPerCountry=[8..20], citiesPerProvince=[2..8], mapSize, landRatio, mountainFactor, resourceRichness… }` — tất cả chỉnh được (GDD II.1).

Mọi bước dùng **RNG riêng theo luồng** (`rng.Stream("terrain")`, `"countries"`…) để đổi một bước không làm thay đổi bước khác.

### 6.1 Các bước (pipeline)

1. **Cell graph**: sinh điểm bằng Poisson-disk, nới lỏng Lloyd 2–3 vòng, dựng Voronoi/Delaunay. Cho ra ô đều tự nhiên, không có lưới vuông.
2. **Địa hình**: heightmap bằng fBm/ridged noise có mặt nạ lục địa (domain warping), cộng các dãy núi tạo bằng đường “đứt gãy” ngẫu nhiên; ngưỡng mực nước biển theo `landRatio`. Xác định biển, hồ, bờ biển.
3. **Xói mòn (nhẹ)**: vài trăm vòng thermal/hydraulic erosion trên heightfield để có thung lũng tự nhiên.
4. **Khí hậu**: nhiệt độ theo vĩ độ + độ cao; độ ẩm theo khoảng cách biển + gió thịnh hành + hiệu ứng bóng mưa phía sau núi.
5. **Thủy văn**: tính hướng dốc → *flow accumulation* → sông bắt đầu khi lượng nước vượt ngưỡng; lấp trũng thành hồ; sông chảy ra biển. Lưu sông như đường polyline trên cạnh ô.
6. **Biome**: bảng tra (nhiệt độ × độ ẩm × độ cao) → rừng, đồng cỏ, sa mạc, tundra, đầm lầy, núi…
7. **Tài nguyên** (GDD IV.2): mỗi loại có luật sinh riêng, phân bố **không đều**:
   - Lương thực/nước: theo độ phì + sông.
   - Gỗ: theo biome rừng.
   - Than/sắt/đồng/nhôm/kim loại quý: noise có ngưỡng gắn với vùng núi/đứt gãy, mỗi mỏ có trữ lượng và độ giàu.
   - Dầu/khí: bể trầm tích ở đồng bằng thấp và thềm biển.
   Đầu ra: `ResourceDeposit { type, cellId, richness, reserve }`.
8. **Chọn vị trí quốc gia**: chọn N hạt giống bằng best-candidate theo điểm *khả cư* (nước, đất phì, địa hình thấp) với khoảng cách tối thiểu; kích thước quốc gia lấy từ phân phối lệch (có nước lớn, nước nhỏ).
9. **Mở rộng lãnh thổ**: flood-fill nhiều nguồn theo **chi phí** (leo núi, vượt sông đắt, qua biển không đi được) tới khi phủ hết đất liền → biên giới tự bám theo sông/núi.
10. **Tỉnh**: trong mỗi quốc gia, chọn k hạt giống tỉnh (k ∈ [8,20], tỉ lệ với diện tích/khả cư), lại flood-fill/k-means trên cell graph, đảm bảo mỗi tỉnh **liên thông**.
11. **Thành phố**: tính điểm phù hợp cho từng ô = gần nước ngọt + đất phì + địa hình bằng + gần giao lộ tiềm năng; chọn 2–8 thành phố/tỉnh với khoảng cách tối thiểu. Cảng chỉ ở ô ven biển/sông đủ rộng; khu công nghiệp ưu tiên gần mỏ và tuyến vận tải.
12. **Thủ đô**: mỗi quốc gia chọn 1 thành phố theo điểm kết hợp (trung tâm địa lý, giao thông, khả cư, tùy chọn gần biển/chiến lược theo tính cách quốc gia).
13. **Mạng đường nền**: nối thành phố bằng đường tối ưu chi phí (A* trên cell graph, tránh dốc/sông) → sinh đường quốc lộ, cầu ở nơi cắt sông.
14. **Đặt tên & quốc kỳ**: tên sinh bằng bộ âm tiết/Markov theo “văn hóa” của quốc gia; quốc kỳ sinh theo thuật toán mẫu hình học + bảng màu từ seed.
15. **Kiểm tra hợp lệ (Validator)**: xem 6.2.

### 6.2 Validator (bắt buộc đạt mới cho vào game)
- Mọi quốc gia có đúng 1 thủ đô, thủ đô nằm trong lãnh thổ.
- Mọi tỉnh liên thông; mọi thành phố thuộc đúng 1 tỉnh; mọi tỉnh thuộc đúng 1 quốc gia.
- Thành phố cảng tiếp giáp nước; không thành phố nào dưới mực nước/trên đỉnh dốc quá ngưỡng.
- Mỗi quốc gia đủ tài nguyên nền để khởi động (lương thực + nước + ít nhất một nguồn năng lượng hoặc khả năng nhập khẩu).
- Không có quốc gia/tỉnh dưới ngưỡng kích thước tối thiểu.
- Nếu thất bại: tăng bộ đếm thử với seed phụ và sinh lại bước lỗi (tối đa N lần), ghi log.

### 6.3 Hiệu năng mục tiêu
- Sinh thế giới 30 quốc gia: **< 30 giây** trên PC tầm trung (chạy Jobs/Burst, hiển thị thanh tiến trình theo từng bước).

### 6.4 Nghiệm thu Giai đoạn 1
- Cùng seed → thế giới giống hệt (so hash `WorldState`).
- 1.000 seed chạy headless: ≥ 99% qua Validator mà không cần sinh lại quá 3 lần.
- Camera 3 cấp (GDD X) khám phá được thế giới; có lớp bản đồ: địa hình, chính trị, tài nguyên, khí hậu.

---

## 7. ĐÔ THỊ (Giai đoạn 2)

### 7.1 Mô hình
- **Lưới đường là đồ thị**: nút (giao lộ) + cạnh (đoạn đường) có loại (dân cư, đô thị, đại lộ, cao tốc, cầu, hầm), làn, tốc độ, công suất. Đường cong và địa hình tự nhiên được hỗ trợ (spline), không bắt buộc lưới vuông.
- **Phân khu (zoning)**: người chơi tô vùng dọc hai bên đường → ô lô đất (parcel) sinh tự động theo chiều sâu/độ rộng.
- **Công trình**: mỗi lô tự phát triển công trình theo nhu cầu và cấp zone; công trình đặc biệt (bệnh viện, trường…) đặt tay. Mỗi loại có nhiều **biến thể mô hình/kiến trúc** chọn theo seed + khu vực + cấp độ để tránh lặp lại (GDD I.3).

### 7.2 Mô phỏng dân cư
Không mô phỏng từng người ở L0/L1. Dùng **nhóm dân cư (cohort)** theo khu phố: số hộ, thu nhập, tuổi, trình độ.
- Nhu cầu: nhà ở, việc làm, thực phẩm, y tế, giáo dục, an ninh, giải trí, giao thông.
- Mỗi nhu cầu = *cung / cầu theo bán kính phục vụ*, tính bằng lan truyền trên đồ thị đường (dùng Dijkstra nhiều nguồn có giới hạn khoảng cách).
- Chỉ số: thất nghiệp, tăng dân số, thu nhập, chất lượng sống, hài lòng, tội phạm, ô nhiễm; cập nhật hàng tuần.

### 7.3 Giao thông
- Luồng hàng/người mô phỏng bằng **phân bổ lưu lượng** trên đồ thị (traffic assignment đơn giản, lặp vài vòng) → mật độ → thời gian di chuyển → ảnh hưởng năng suất và hậu cần.
- Phương tiện hiển thị (L2) là agent nhẹ lấy mẫu từ lưu lượng, **không** quyết định kết quả mô phỏng.

### 7.4 Nghiệm thu Giai đoạn 2
- Xây đường, phân khu, công trình; dân cư vào ở và có việc làm; dịch vụ ảnh hưởng chỉ số.
- 60 FPS ổn định với thành phố ~20.000 công trình trên máy tầm trung (mục tiêu; đo ở Spike).

---

## 8. KINH TẾ (Giai đoạn 3)

### 8.1 Cấu trúc
- **Tài nguyên & hàng hóa**: danh mục trong file dữ liệu (`resources.json`, `goods.json`) – không hard-code.
- **Chuỗi sản xuất**: `Recipe { inputs[], outputs[], laborType, laborAmount, techRequired, time }`. Ví dụ: quặng sắt + than → thép; thép + điện tử → máy móc; thép + máy móc → xe quân sự.
- **Sản lượng** = f(nhân lực, nguyên liệu sẵn có, công nghệ, hạ tầng, vốn) với hiệu suất bị chặn bởi yếu tố khan hiếm nhất.

### 8.2 Quy mô tính toán
- Mô phỏng **theo tỉnh**: mỗi tỉnh gộp tòa nhà cùng loại thành `ProductionCapacity`. Thành phố chi tiết chỉ khi ở L1.
- Vận chuyển hàng: dòng chảy giữa tỉnh trên đồ thị đường/ray/cảng với chi phí và công suất; thiếu công suất → tắc nghẽn → giá địa phương tăng.

### 8.3 Ngân sách quốc gia
Thu: thuế dân cư, doanh nghiệp, thương mại, thuế quan. Chi: chính phủ, quốc phòng, giáo dục, y tế, hạ tầng, R&D, trả lãi nợ. Hỗ trợ vay nợ/lạm phát cơ bản; thâm hụt kéo dài → khủng hoảng (ảnh hưởng ổn định).

### 8.4 Thương mại & giá
- Giá mỗi hàng hóa **theo thị trường vùng**; cập nhật hàng tuần theo cung/cầu và chi phí vận chuyển (mô hình đơn giản kiểu *price adjustment*: `giá += k · (cầu − cung)/cầu`, có giới hạn biên).
- Hợp đồng thương mại giữa quốc gia là `Treaty` kèm lượng/giá/thời hạn.

### 8.5 Nghiệm thu Giai đoạn 3
- Cân bằng ngân sách có thể đạt được; thay đổi thuế gây hiệu ứng đo được.
- Thiếu tài nguyên làm chuỗi sản xuất giảm sản lượng; nhập khẩu bù được.
- Kiểm tra bảo toàn: tổng sản xuất − tiêu thụ − tồn kho − xuất/nhập khớp (test tự động mỗi tick).

---

## 9. AI QUỐC GIA (Giai đoạn 4)

### 9.1 Kiến trúc: **Utility AI phân tầng**

```
Strategic Layer (5–10 ngày/lần)  → chọn mục tiêu quốc gia
   Mục tiêu: Tăng trưởng kinh tế · Đảm bảo lương thực · Củng cố quốc phòng ·
             Tìm đồng minh · Mở rộng lãnh thổ · Nghiên cứu công nghệ · …
Operational Layer (hàng tuần)    → phân bổ ngân sách, quyết định xây dựng/sản xuất
Tactical Layer (hàng ngày)       → điều quân, xử lý khủng hoảng
```

Mỗi hành động có **điểm tiện ích**:
`Utility = Σ (trọng số_tính_cách × điểm_nhu_cầu × điều_chỉnh_ngữ_cảnh)`

### 9.2 Tính cách (GDD VII.1)
Mỗi quốc gia AI có vector trọng số, ví dụ:

| Tính cách | Thiên về |
|---|---|
| Thương mại | thuế quan thấp, hiệp ước thương mại, cảng |
| Công nghiệp | đầu tư nhà máy, tài nguyên |
| Quân sự | ngân sách quốc phòng, tuyển quân |
| Công nghệ | R&D, giáo dục |
| Trung lập | tránh liên minh/chiến tranh |
| Bành trướng | chiếm lãnh thổ khi có lợi thế |
| Cân bằng | trọng số đều |

Tính cách chỉ là **điểm khởi đầu**; trọng số dịch chuyển theo hoàn cảnh (thua trận → ưu tiên phòng thủ).

### 9.3 Đầu vào nhận thức
AI chỉ dùng thông tin **có thể biết** (không gian lận bằng dữ liệu ẩn) ở mức độ tình báo hợp lý: sức mạnh kinh tế/quân sự ước lượng của láng giềng, quan hệ, hiệp ước, nhu cầu nội tại (lương thực, năng lượng, nợ), mối đe dọa.

### 9.4 Hành vi thích nghi (GDD VII.3)
Biểu diễn bằng luật tiện ích, không kịch bản cố định. Ví dụ:
- Thiếu lương thực → tăng điểm cho `nhập khẩu lương thực` và `mở rộng nông nghiệp`; chọn cái rẻ hơn theo điều kiện thực.
- Nguy cơ chiến tranh (láng giềng mạnh + quan hệ xấu) → tăng điểm `ngân sách quốc phòng`, `tìm đồng minh`.
- Kinh tế suy yếu → tăng điểm `cắt chi tiêu`, `điều chỉnh thuế`.
- Lợi thế kinh tế → tăng điểm `mở rộng thương mại/ảnh hưởng`.

### 9.5 Kiểm soát chi phí & công bằng
- Mỗi tick chỉ một phần AI chạy chiến lược (xen kẽ); ngân sách CPU mỗi AI có giới hạn.
- AI và người chơi dùng **cùng** `Command` và luật; không có “bonus ẩn” ngoài độ khó tùy chọn công khai.

### 9.6 Công cụ
- **AI Debugger**: xem điểm tiện ích từng hành động, lý do chọn, lịch sử quyết định.
- **Sandbox headless**: chạy 30 AI × N năm game không có người chơi để quan sát ổn định, độ đa dạng lịch sử và phát hiện sụp đổ/vô hạn.

### 9.7 Nghiệm thu Giai đoạn 4
- 30 AI tự chạy 50 năm game không crash, không rơi vào trạng thái vô nghĩa (nợ vô hạn, dân số âm, quốc gia “đứng yên”).
- Các seed khác nhau cho **lịch sử khác nhau** (độ đa dạng đo bằng số quốc gia còn lại, bản đồ cuối).

---

## 10. QUÂN ĐỘI, NGOẠI GIAO, CHIẾN TRANH (Giai đoạn 5–7)

### 10.1 Quân đội
- **Nhân lực**: nghĩa vụ (rẻ, lấy từ lực lượng lao động dân sự → giảm sản lượng) vs chuyên nghiệp (lương cao, kinh nghiệm cao).
- **Đơn vị** ở mức **sư đoàn/hạm đội/phi đội** trên bản đồ chiến lược (không mô phỏng từng binh sĩ trừ khi vào chiến trường 3D).
- Chỉ số (GDD V.3): quân số, kinh nghiệm, tinh thần, huấn luyện, trang bị, hậu cần, khả năng tác chiến.
- Trang bị là hàng hóa từ **công nghiệp quốc phòng** (chuỗi sản xuất mục 8), cần công nghệ và nguyên liệu.
- **Hậu cần**: đơn vị tiêu hao *supply*; cung ứng chạy theo đường/ray/cảng từ kho; đứt tuyến → giảm tinh thần/sức chiến đấu.

### 10.2 Công nghệ (GDD VI)
- 5 nhánh; mỗi công nghệ là nút trong đồ thị: `Tech { prerequisites[], cost (ngân sách+nhân lực+thời gian+nguyên liệu), unlocks[] }`.
- Điều kiện cần trung tâm nghiên cứu; tốc độ nghiên cứu = f(đầu tư, nhân lực chuyên môn, giáo dục).
- Công nghệ khóa công trình/thiết bị ở **tầng dữ liệu** (cờ `RequiresTech`), không rải `if` trong code.

### 10.3 Ngoại giao
- **Quan hệ song phương**: điểm quan hệ liên tục (−100..+100) + trạng thái rời rạc (hòa bình, trung lập, đối tác thương mại, đồng minh, liên minh quân sự, đối địch).
- **Hiệp ước** (`Treaty`): loại, các bên, điều khoản, thời hạn, điều kiện vi phạm, hậu quả (mất uy tín).
- **Đánh giá của AI đối với người chơi** (GDD VIII.4): hàm điểm từ sức mạnh kinh tế/quân sự, quan hệ trước, độ tin cậy (lịch sử phá hiệp ước), hiệp ước hiện có, lợi ích chiến lược.
- Đàm phán: người chơi đưa đề nghị → AI tính điểm *chấp nhận* = (lợi ích nhận − lợi ích mất) cộng yếu tố quan hệ/tin cậy; có thể phản đề nghị.

### 10.4 Chiến tranh – hai lớp (GDD IX)
**Lớp chiến lược (bắt buộc, đầy đủ):** trên đồ thị tỉnh.
- Tuyên chiến, điều quân, chiếm đóng (tiến độ chiếm theo ngày, phụ thuộc tương quan lực lượng), phong tỏa, hỗ trợ đồng minh, đình chiến/hòa bình.
- **Giải quyết giao tranh tự động** theo công thức nhiều yếu tố (không chỉ quân số):
  `Sức mạnh = Quân_số_hiệu_dụng × Trang_bị × Công_nghệ × Kinh_nghiệm × Tinh_thần × Địa_hình × Hậu_cần × Chỉ_huy`
  cộng nhiễu có kiểm soát (RNG tất định); thương vong và thay đổi tinh thần tính từ chênh lệch sức mạnh.
- **Hậu quả** (GDD IX.4) nối thẳng vào mô phỏng: thương vong → giảm lực lượng lao động; tỉnh bị chiếm → mất sản lượng/thuế; hạ tầng hư hỏng; ngân sách khủng hoảng; di dân; đổi biên giới; bất ổn.
- **Đổi chủ quyền** là một thao tác nguyên tử `TransferProvince(provinceId, newOwner)` cập nhật: chủ tỉnh, chỉ mục quốc gia↔tỉnh, thành phố, công trình, quân đồn trú, bản đồ chính trị, sự kiện. Có test bất biến sau mỗi lần đổi.
- Thủ đô bị chiếm → nghi thức chuyển thủ đô/khủng hoảng; quốc gia mất hết tỉnh → tan rã; sáp nhập/tách quốc gia đều qua thao tác chuẩn.

**Lớp chiến thuật 3D (Giai đoạn 7, tùy chọn bật/tắt):** chiến trường sinh từ địa hình thật của tỉnh; người chơi có thể *quan sát/điều khiển* hoặc để tự động giải quyết. Kết quả trả về lớp chiến lược theo cùng cấu trúc thương vong/kiểm soát. **[QUYẾT ĐỊNH MỞ #2]** – chiến thuật 3D điều khiển trực tiếp hay chỉ quan sát ở phiên bản đầu.

### 10.5 Nghiệm thu
- Một cuộc chiến đầy đủ (tuyên chiến → chiếm tỉnh → hòa bình) làm thay đổi biên giới, kinh tế, dân số và được phản ánh trên bản đồ chính trị.
- Bất biến dữ liệu luôn đúng sau mọi chuyển giao lãnh thổ (xem mục 12).

---

## 11. GIAO DIỆN VÀ CAMERA

### 11.1 Ba cấp quan sát (GDD X)
| Cấp | Dữ liệu hiển thị | Kỹ thuật |
|---|---|---|
| 1 – Thế giới | biên giới, thủ đô, thành phố lớn, ngoại giao, chiến sự | Mesh thế giới đơn giản hóa + overlay, texture bản đồ chính trị |
| 2 – Tỉnh | ranh giới tỉnh, tài nguyên, dân số, đường | Terrain chunk độ chi tiết trung bình + icon/biểu tượng |
| 3 – Thành phố 3D | công trình, đường, xe cộ | Terrain chi tiết + GPU instancing + LOD mô hình |

### 11.2 Kỹ thuật hiển thị
- **Chunk streaming** theo camera; LOD địa hình (clipmap/quadtree).
- **GPU instancing** + LOD cho công trình, cây, phương tiện; occlusion culling.
- **Lớp bản đồ** (chính trị, địa hình, tài nguyên, kinh tế, ngoại giao…) là shader/texture overlay sinh từ `WorldState`.
- Camera: zoom liên tục, xoay, di chuyển tự do; chuyển mượt giữa các cấp.
- Ánh sáng, thời tiết, ngày/đêm: chu kỳ ngày game gắn với thời gian hiển thị (không ảnh hưởng luật mô phỏng, trừ hiệu ứng thời tiết đã khai báo).

---

## 12. LƯU TRỮ VÀ TÍNH TOÀN VẸN DỮ LIỆU

### 12.1 Lưu game (GDD XI.4)
Một tệp lưu gồm:
- Header: phiên bản định dạng, phiên bản game, seed, thời gian game, ảnh chụp thu nhỏ, hash.
- **Cấu hình & seed** (tạo lại địa hình gốc rất rẻ) + **delta thay đổi** (công trình, đường, đổi chủ, tài nguyên đã khai thác) thay vì lưu lại toàn bộ địa hình.
- Trạng thái các bảng: quốc gia, tỉnh, thành phố, công trình, quân đội, hiệp ước, quan hệ, chiến tranh, công nghệ, thị trường.
- **Trạng thái AI** (mục tiêu, trọng số đã dịch chuyển, bộ nhớ quyết định) và **trạng thái RNG từng luồng**.
- **Lịch sử sự kiện** (nén) – để hiển thị lịch sử thế giới.

Nhiều ô lưu, tự động lưu (xoay vòng N bản), lưu bất đồng bộ (chụp snapshot rồi ghi nền → không giật khung hình).

### 12.2 Phiên bản & di trú
- Mỗi thay đổi định dạng tăng `SaveVersion` kèm hàm migrate; test tự động nạp các bản lưu cũ lưu trong repo.

### 12.3 Bất biến phải luôn đúng (kiểm tra tự động)
- Mỗi tỉnh có đúng 1 chủ; mỗi thành phố thuộc đúng 1 tỉnh; mỗi công trình thuộc đúng 1 thành phố.
- Chỉ mục “tỉnh theo quốc gia” khớp `Province.OwnerCountryId`.
- Quốc gia còn tồn tại ⇒ có ≥ 1 tỉnh và 1 thủ đô nằm trong lãnh thổ.
- Không giá trị âm vô nghĩa (dân số, kho), không NaN/Inf.
- Kiểm tra bảo toàn hàng hóa (mục 8.5).
- Chạy ở chế độ Debug mỗi tick; ở Release chạy mỗi N tick/ trước khi lưu.

---

## 13. HIỆU NĂNG

### 13.1 Ngân sách mục tiêu (PC tầm trung: 6 nhân, 16 GB RAM, GPU tầm trung)
| Hạng mục | Mục tiêu |
|---|---|
| Khung hình | 60 FPS ở cấp 3; ≥ 60 FPS ở cấp 1–2 |
| Tick mô phỏng (1 ngày game, 30 quốc gia) | ≤ 15 ms trung bình, ≤ 50 ms đỉnh (trên thread mô phỏng) |
| Sinh thế giới | < 30 s |
| Lưu game | < 2 s, không giật khung hình |
| Bộ nhớ | < 6 GB ở cấu hình mặc định |

### 13.2 Kỹ thuật
- Dữ liệu dạng mảng liên tục + Burst/Jobs; tránh cấp phát GC trong tick.
- Mô phỏng phân cấp (mục 5.2); AI xen kẽ; cache đường đi; cập nhật tăng dần (dirty flag).
- Pathfinding: A* phân cấp (tỉnh → thành phố → đường) + cache; luồng hàng dùng thuật toán luồng đơn giản trên đồ thị đã nén.
- Cấu hình chất lượng đồ họa nhiều mức.

### 13.3 Technical Spike (làm TRƯỚC khi chốt engine – GDD XI.1)
Thời lượng đề xuất: 2–3 tuần. Cần chứng minh trên Unity:
1. Sinh cell graph ~300k ô + heightmap + sông trong ngân sách thời gian.
2. Render terrain chunk streaming zoom từ cấp 1 → 3 mà không giật.
3. Tick mô phỏng giả lập 30 quốc gia/ ~3.000 tỉnh/thành phố trong ngân sách.
4. Instancing 20.000 công trình ở cấp 3.
Nếu thất bại ở điểm nào → thu hẹp quy mô hoặc đánh giá lại engine.

---

## 14. KIỂM THỬ

- **Unit test (Core)**: công thức kinh tế, giao tranh, tiện ích AI, thao tác chuyển giao lãnh thổ.
- **Property/invariant test**: sinh nhiều seed, chạy mô phỏng ngắn, kiểm tra mục 12.3.
- **Golden test tất định**: seed cố định → hash trạng thái sau K tick phải khớp (phát hiện thay đổi hành vi ngoài ý muốn).
- **Soak test AI**: 30 AI × 100 năm game headless, thu thập thống kê (số quốc gia, GDP, chiến tranh) để cân bằng.
- **Test lưu/nạp**: lưu → nạp → chạy tiếp phải trùng với chạy liên tục (cùng seed, cùng hash).
- **Test hiệu năng tự động**: benchmark tick/sinh thế giới, cảnh báo nếu vượt ngân sách.

---

## 15. LỘ TRÌNH TRIỂN KHAI KỸ THUẬT

| GĐ | Nội dung | Sản phẩm bàn giao | Tiêu chí hoàn thành |
|---|---|---|---|
| 0 | Technical Spike + chốt engine | Báo cáo spike, repo khởi tạo, CI | 4 mục spike đạt |
| 1 | World Generator | Thế giới khám phá được, 3 cấp camera, lớp bản đồ, Validator | Mục 6.4 |
| 2 | City Builder | Xây đường/zone/công trình, dân cư | Mục 7.4 |
| 3 | Economy | Ngân sách, sản xuất, tài nguyên, thương mại | Mục 8.5 |
| 4 | AI Countries | AI tự phát triển, AI Debugger, Sandbox | Mục 9.7 |
| 5 | Military & Tech | Tuyển quân, doanh trại, trang bị, nghiên cứu | Đơn vị sản xuất/di chuyển trên bản đồ chiến lược |
| 6 | Diplomacy | Quan hệ, hiệp ước, đàm phán | AI phản ứng thực tế với đề nghị |
| 7 | Warfare | Tuyên chiến → chiếm đóng → hòa bình, chiến trường 3D | Mục 10.5 |
| 8 | Hoàn thiện | UI/âm thanh/VFX, tối ưu, lưu/nạp, cân bằng | Toàn bộ ngân sách hiệu năng + test |

Quy tắc: **không bắt đầu giai đoạn n+1 khi giai đoạn n chưa qua tiêu chí hoàn thành** và chưa có test bất biến cho dữ liệu của giai đoạn đó.

---

## 16. RỦI RO VÀ GIẢM THIỂU

| Rủi ro | Mức | Giảm thiểu |
|---|---|---|
| Phạm vi quá lớn (tương đương nhiều game gộp lại) | Rất cao | Làm tuần tự theo giai đoạn; mỗi GĐ phải chơi được; cắt chiến thuật 3D điều khiển trực tiếp nếu trễ |
| Hiệu năng bản đồ lớn + mô phỏng nhiều thực thể | Cao | Mô phỏng phân cấp, Technical Spike, benchmark tự động |
| AI nhàm chán hoặc “vô nghĩa” | Cao | Utility AI + AI Debugger + soak test + cân bằng bằng dữ liệu |
| Mất đồng bộ dữ liệu khi đổi chủ quyền | Cao | Thao tác nguyên tử duy nhất + test bất biến |
| Cân bằng kinh tế khó | Trung bình | Mọi hằng số trong file dữ liệu; công cụ vẽ biểu đồ; soak test |
| Khối lượng nội dung đồ họa (công trình đa dạng) | Cao | Công trình mô-đun (kết hợp thành phần), sinh biến thể thủ tục, thư viện asset kế thừa |
| Lưu game phình to | Trung bình | Lưu seed + delta, nén, snapshot nền |

---

## 17. QUYẾT ĐỊNH MỞ

1. **Engine**: Unity (khuyến nghị) hay Unreal – chốt sau Technical Spike.
2. **Chiến thuật 3D**: chỉ quan sát hay điều khiển trực tiếp ở bản đầu?
3. **Nền tảng mục tiêu**: chỉ PC (Windows) hay thêm macOS/Linux/console? (ảnh hưởng ngân sách hiệu năng)
4. **Điều kiện chiến thắng**: có điều kiện thắng rõ ràng (điểm, thống trị, kinh tế) hay chế độ sandbox mở?
5. **Thời đại/bối cảnh công nghệ**: hiện đại, hay có dòng thời gian nhiều thời đại?
6. **Nguồn nội dung đồ họa**: tự làm asset, mua asset, hay sinh thủ tục là chính?

---

## 18. BƯỚC TIẾP THEO

1. Xác nhận các **quyết định mở** (đặc biệt #1, #3, #4).
2. Thực hiện **Giai đoạn 0 – Technical Spike** (mục 13.3).
3. Khởi tạo cấu trúc dự án Unity theo mục 3.1 và CI chạy test Core.
4. Bắt đầu **Giai đoạn 1 – World Generator** theo mục 6 với `WorldConfig` và `Validator` làm việc đầu tiên.

**END OF TDD – VERSION 1.0**
