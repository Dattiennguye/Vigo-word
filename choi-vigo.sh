#!/usr/bin/env bash
# Chạy VIGO trên Linux/macOS: mở trình duyệt tới http://localhost:5077
cd "$(dirname "$0")"
( sleep 3; (xdg-open http://localhost:5077 || open http://localhost:5077) >/dev/null 2>&1 ) &
dotnet run --project src/Vigo.Game -c Release
