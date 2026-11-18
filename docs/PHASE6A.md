# GĐ6a – AI đề nghị liên minh/hiệp ước tới người chơi

## Vì sao
Trước đây AI bỏ qua nước người chơi khi lập liên minh. Giờ AI chủ động gửi **đề nghị** mà người chơi tự quyết (không bao giờ tự ký thay người chơi).

## API (`WarSim`, file Military/Offers.cs)
- `Offers`, `OffersFor(human)`, `SendOffer(from,to,type)` (chỉ gửi tới nước người chơi), `AcceptOffer(id)`, `DeclineOffer(id)`, `PruneOffers()`.
- `DiplomacyAI.Think()` (mỗi 26 tuần, khi `Treaties=true`) gọi `ThinkOffers`; mỗi tuần dọn đề nghị hết hạn/không còn hợp lệ.

## Quy tắc
- Liên minh: AI là láng giềng, thiện cảm với người chơi ≥ 30, AI chưa đủ 3 đồng minh, không đang chiến tranh.
- Hiệp ước không xâm lược: AI thấy người chơi mạnh hơn 1,5× (sợ bị tấn công) hoặc thiện cảm ≥ 10, và thiện cảm > −10.
- Mỗi AI tối đa một đề nghị chờ tới một người chơi mỗi loại; đề nghị sống 26 tuần.
- Chấp nhận: kiểm tra lại tính hợp lệ (đồng minh tối đa 3 cả hai bên, không ký với kẻ đang đánh đồng minh của mình, chưa có chiến tranh); hợp lệ thì ký, thiện cảm +20 (liên minh) / +8 (hiệp ước). Từ chối: thiện cảm −3.
- Lưu trong `SaveGame` phiên bản 6 (cả `NextOfferId`).

## Hạn chế
- Các ngưỡng 30/10/1,5×/−3 là tham số tôi đặt, chưa hiệu chỉnh theo trải nghiệm chơi.
- Người chơi chưa thể *yêu cầu* AI điều khoản cụ thể (chỉ chấp nhận/từ chối); AI chưa dùng `ProposeTerms`.
- Chưa có giao diện — lớp Unity sẽ hiển thị hộp thư đề nghị.
