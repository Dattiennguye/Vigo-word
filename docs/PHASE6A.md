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

## AI áp điều kiện hòa đàm (`DiplomacyAI.Terms`, mặc định bật)
- Mỗi 13 tuần, với chiến tranh đã ≥ 26 tuần mà một bên AI có sức mạnh ≥ 1,5× bên kia: bên mạnh gọi `WarSim.ProposeTerms` đòi tối đa 3 tỉnh biên giới của đối phương (đủ trần `ProposeTerms` cho phép; thủ đô chỉ khi tỷ lệ ≥ 3) và bồi thường 50% trần.
- Không áp lên nước người chơi (cả hai chiều); với người chơi vẫn dùng `ProposePeace`/`ProposeTerms` thủ công.
- Đo: seed 75, 1500 tuần, 10 lần áp điều kiện (tắt: 0); bất biến thế giới giữ nguyên.
- Hạn chế: bên thua AI bị áp đặt, không thương lượng; ngưỡng 1,5×/26 tuần/50% là tham số tôi đặt.

## Hiệp ước thương mại (`TreatyType.Trade`)
- `WarSim.ProposeTrade/AcceptsTrade/HasTrade/TradePartners`; tối đa 4 đối tác mỗi nước, thiện cảm đối phương ≥ 0, hiệu lực 260 tuần, tự hủy khi hai nước có chiến tranh. AI ký với láng giềng thiện cảm ≥ 10 mỗi 26 tuần; gửi đề nghị tới người chơi qua cùng hệ thống `Offers` (loại `Trade`).
- Tác động thật lên kinh tế: `EconomySim.TradePref[i,j]` (hiệp ước thương mại ×3, liên minh ×2, còn lại ×1) được đặt từ hiệp ước ngay trước mỗi vòng thương mại (`BeforeTrade`). Nó (a) lệch lựa chọn nhà xuất khẩu của người mua, và (b) khi nhà xuất khẩu khan hàng, chia hàng theo yêu cầu × ưu tiên. Hàng hóa vẫn chuyển zero-sum và qua sổ cái.
- Đo: nhà xuất khẩu khan lương thực, người mua có hiệp ước nhận kho 78486 so với 26763 khi không có (một mức khan hiếm trong test); ở mọi mức khan hiếm đã thử, hiệp ước không làm người mua thiệt.
- Không có thuế quan/miễn thuế (kinh tế chưa mô hình thuế nhập khẩu); đây là *ưu tiên tiếp cận hàng*, không phải giảm giá. `TradePref` không lưu vì suy ra lại từ hiệp ước (nạp bản lưu cho kết quả y hệt, test).

## Hải quân / đổ bộ (đường biển)
- **Phát hiện khi đo**: trong các thế giới sinh ra không có nước nào bị cô lập hoàn toàn (đều có láng giềng đất liền), nên hải quân chỉ thêm khoảng 0–6 cặp nước kề biển mỗi thế giới (lệnh `--seadiag N` của CLI). Đây là mở rộng chiến trường chứ không phải sửa lỗ hổng lớn.
- `WarSim.SeaLinks[p]`: liên kết giữa tỉnh ven biển cách nhau ≤ 6 khoảng cách ô (hình học tĩnh, đối xứng); `SeaNeighbors(c)`/`SeaAdjacent(a,b)`: nước kề biển theo chủ sở hữu hiện tại, không tính láng giềng đất liền.
- Luật: tuyên chiến qua biển khi cả hai kề biển **và bên tấn công có cảng** (`Country.HasPort`). Tiền tuyến đổ bộ: tấn công ×0,5 (`AmphibiousAttack`), phòng thủ ×1,25 (`CoastalDefense`). Tỉnh có cả đường bộ lẫn biển tính là mặt trận đất liền. Tàn phá chiến tranh tính cả mặt trận biển.
- AI hiếu chiến có cảng chỉ đánh qua biển khi áp đảo ≥ 3× (đường bộ vẫn ≥ 1,8×).
- Không có hạm đội riêng (quân số dùng chung với lục quân), chưa có phong tỏa/đánh chìm thương thuyền; hằng số 6/0,5/1,25/3× là tham số tôi đặt, chưa hiệu chỉnh.
- Test: liên kết đối xứng, điều kiện tuyên chiến (cảng, kề biển), áp đảo thì đổ bộ chiếm được tỉnh (15 → 14 tỉnh trong thử nghiệm), ngang sức 60 tuần không chiếm được.
