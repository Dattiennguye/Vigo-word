# Giai đoạn 4c – Liên minh, hiệp ước, hòa đàm

Mã: `src/Vigo.Core/Military/Diplomacy.cs` (phần của `WarSim`), mở rộng `DiplomacyAI.cs`, `WarSim.cs`, `SaveGame.cs` (phiên bản 3).
Test: `src/Vigo.Tests/DiplomacyTests.cs` (8 test; tổng 60 đạt). Chạy thử: `dotnet run --project src/Vigo.Cli -- --war 60 --seed 777`.

## Cơ chế
- **Thiện cảm** (`GetOpinion(i,j)`, −100..100): nền tất định theo seed + tính cách (nước Thương mại/Trung lập/Công nghệ được mến hơn, nước hiếu chiến bị ghét hơn) + phần động phân rã 0.5%/tuần. Tăng: láng giềng hòa bình (không hiếu chiến) dần thân thiện, cùng đánh một kẻ thù, ký hiệp ước/liên minh. Giảm mạnh: bị tuyên chiến (−60), kẻ gây chiến mất thiện cảm với cả thế giới (−8), bội ước (−20 với tất cả, −50/−80 với nạn nhân).
- **Hiệp ước không xâm lược** (260 tuần) và **liên minh** (vô thời hạn, tối đa 3/nước, đối xứng). Có hiệp ước/liên minh thì không tuyên chiến được trừ khi `DeclareWar(..., breakTreaties: true)` (bội ước, có sự kiện `betrayal`). Liên minh tự tan nếu hai bên đánh nhau.
- **Điều kiện chấp nhận (AI)**: liên minh cần thiện cảm ≥ 25, không kết với kẻ đang đánh đồng minh mình, nước hiếu chiến chỉ kết với nước không quá yếu; hiệp ước cần thiện cảm ≥ −10 và bên hiếu chiến không chấp nhận khi đang thấy mồi ngon (mạnh >1.8×).
- **Kêu gọi đồng minh**: khi A đánh B, đồng minh của B vào cuộc chống A (trừ khi A mạnh >3× và đồng minh không hiếu chiến → từ chối, mất thiện cảm); đồng minh của A vào cuộc nếu không ưa B (60%, tất định). Chiến tranh phụ gắn với chiến tranh chính (`MasterA/MasterB`), cấm vận với đồng minh của địch, và kết thúc cùng cuộc chiến chính. Đồng minh cùng chiến tuyến tăng sức mạnh bên mình tới tối đa ×1.8.
- **Răn đe**: AI tính **sức mạnh liên minh** của mục tiêu (kể cả đồng minh nó), nên không tấn công kẻ yếu có đồng minh mạnh.
- **Hòa đàm có điều kiện** (`ProposeTerms`): bên thắng đòi nhượng tỉnh + bồi thường; AI thua chỉ nhận khi bị áp đảo ≥1.3×, số tỉnh ≤ max(1, 12% × (tỉ lệ−1) × số tỉnh), thủ đô chỉ khi ≥3×, bồi thường ≤ 10 tuần thuế tiềm năng, không bao giờ bị xóa sổ qua hòa đàm. Tiền chuyển zero-sum.
- **AI** mỗi 26 tuần: ký hiệp ước với láng giềng hiếu chiến mạnh hơn 1.5×; lập liên minh với láng giềng mến nhất (cộng điểm nếu cùng có một láng giềng hiếu chiến). Người chơi (`SetHuman`) không bị AI điều khiển; đồng minh người chơi chỉ nhận sự kiện `call`, tự quyết `JoinWar`.
- Lưu/nạp đầy đủ (hiệp ước, thiện cảm, chiến tranh phụ, tập hợp người chơi).

## Đo được
3 seed × 60 năm, ~1.2 giây/lần: liên minh tối đa cùng lúc 6 / 11 / 8; còn lại 16 / 21 / 13 nước (so với 10–18 trước khi có liên minh). So trực tiếp trong test: tổng nước còn sau 60 năm **50 có liên minh vs 39 không** — liên minh làm thế giới ít hợp nhất hơn (chỉ 3 seed, chưa đủ để kết luận chắc).

## Phát hiện khi chạy thật
Lần đầu AI ký 59 hiệp ước nhưng **0 liên minh**: thiện cảm nền hiếm khi chạm ngưỡng 25–30 và không có gì làm nó tăng. Đã thêm cơ chế láng giềng hòa bình dần thân thiện → liên minh hình thành (2–11).

## Hạn chế — nói thẳng
- Hằng số (ngưỡng thiện cảm, tốc độ thân thiện, hệ số ×1.8, 12% nhượng tỉnh…) là **tham số tôi đặt**, chưa cân bằng theo trải nghiệm chơi.
- Đồng minh không giáp địch chỉ cộng sức mạnh (×1.3 hệ số), không mất lính và không bị tổn thất — viễn chinh trừu tượng.
- Thiện cảm chưa tính quan hệ kinh tế/thương mại, tôn giáo/văn hóa, uy tín dài hạn; AI chỉ lập liên minh với láng giềng; chưa có hiệp ước thương mại, quyền đi qua, bảo hộ, chư hầu.
- AI chưa tự dùng `ProposeTerms` (khi thắng chỉ dừng ở mục tiêu chiến tranh); chưa có hải quân; người chơi chưa có giao diện/luồng hỏi–đáp cho lời đề nghị từ AI (cần lớp UI).
- Liên minh chỉ phòng thủ/viễn chinh tự động; không có cơ chế yêu cầu giúp đỡ kinh tế hay quân sự có chọn lọc.
