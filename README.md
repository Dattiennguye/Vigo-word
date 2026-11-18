# VIGO: Empire & Civilization

Game mô phỏng xây dựng quốc gia, quản lý thành phố, kinh tế, công nghệ, quân đội, ngoại giao và chiến tranh. Chơi hoàn toàn offline, các quốc gia còn lại do AI điều khiển.

## Tài liệu
- [GDD – Game Design Document v1.0](docs/GDD.md)
- [TDD – Technical Design Document v1.0](docs/TDD.md)

## Trạng thái
Giai đoạn 1 (World Generator): [docs/PHASE1.md](docs/PHASE1.md). Giai đoạn 2 (City Builder, lõi mô phỏng): [docs/PHASE2.md](docs/PHASE2.md). Giai đoạn 3 (Kinh tế): [docs/PHASE3.md](docs/PHASE3.md). Lưu/nạp/quay lại: [docs/SAVE.md](docs/SAVE.md). AI quốc gia (ngân sách): [docs/PHASE4A.md](docs/PHASE4A.md). Quân đội/ngoại giao/chiến tranh: [docs/PHASE4B.md](docs/PHASE4B.md). Tất cả đã có lõi chạy được và có test (52 test); chưa có lớp Unity. Bước tiếp theo: AI quốc gia + quân sự/ngoại giao, hoặc Technical Spike Unity (Windows + Android). Trạng thái bàn giao: [docs/STATUS.md](docs/STATUS.md).

## Chạy thử lõi sinh thế giới
```bash
dotnet run -c Release --project src/Vigo.Tests            # test
dotnet run -c Release --project src/Vigo.Cli -- --seed 1  # sinh thế giới + xuất bản đồ vào ./out
dotnet run -c Release --project src/Vigo.Cli -- --city capital --seed 31  # mô phỏng một thành phố 4 năm + ảnh
```
