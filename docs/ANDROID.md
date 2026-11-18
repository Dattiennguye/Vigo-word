# Bản Android (APK)

Ứng dụng **độc lập, chơi offline**: một WebView hiển thị giao diện HTML5, còn lõi mô phỏng C# (`Vigo.Core` + `Vigo.Api`) chạy ngay trong ứng dụng — không cần máy tính, máy chủ hay mạng.

## Tải APK
Vào trang **Releases** của repo → bản `VIGO Empire — APK mới nhất` → tải `VIGO-Empire.apk` và cài (cho phép "cài từ nguồn không rõ").
Hoặc Actions → workflow *Build VIGO Empire APK* → artifact `VIGO-Empire-apk`.

## Cách hoạt động
- `src/Vigo.Api`: API trò chơi thuần C# (`GameApi.Handle(method, url, json)`), dùng chung cho bản trình duyệt (Kestrel) và Android.
- `src/Vigo.Android`: Activity toàn màn hình, nạp `wwwroot` từ assets qua `https://vigo.local`, cầu nối JS→C# (`VigoNative.call`); kết quả trả qua `/__r/{id}`.
- Tự lưu khi thoát ứng dụng (`tudongluu`) và mỗi 4 năm trong game; file lưu nằm trong bộ nhớ riêng của ứng dụng.
- Nút Back: đóng thành phố/hộp thoại, nếu không còn thì thu nhỏ ứng dụng.
- Chỉ build kiến trúc arm64 (hầu hết điện thoại đời 2016+).

## Build
GitHub Actions tự chạy bộ test rồi đóng gói mỗi lần push lên `main` (`.github/workflows/build-apk.yml`).
APK ký bằng keystore cá nhân `android/vigo.keystore` (mật khẩu `vigo-empire`) để các bản sau cài đè được bản trước. Muốn phát hành công khai, hãy tạo keystore riêng và đưa vào GitHub Secrets.

## Giới hạn đã biết
- Chưa có âm thanh; chưa thử trên nhiều dòng máy.
- Ván lớn (60.000 ô) sinh thế giới có thể mất vài giây đến chục giây trên máy yếu.
