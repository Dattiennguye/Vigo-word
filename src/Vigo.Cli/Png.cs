using System;
using System.IO;
using System.IO.Compression;

namespace Vigo.Cli
{
    internal static class Png
    {
        private static readonly uint[] Table = MakeTable();
        private static uint[] MakeTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; t[n] = c; }
            return t;
        }
        private static uint Crc(byte[] type, byte[] data)
        {
            uint c = 0xFFFFFFFFu;
            foreach (byte b in type) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
            foreach (byte b in data) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }
        private static void Chunk(Stream s, string type, byte[] data)
        {
            var tb = System.Text.Encoding.ASCII.GetBytes(type);
            WriteBE(s, (uint)data.Length); s.Write(tb, 0, 4); s.Write(data, 0, data.Length); WriteBE(s, Crc(tb, data));
        }
        private static void WriteBE(Stream s, uint v) { s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }

        public static void Save(string path, byte[] rgb, int w, int h)
        {
            using var fs = File.Create(path);
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            var ihdr = new byte[13];
            ihdr[0] = (byte)(w >> 24); ihdr[1] = (byte)(w >> 16); ihdr[2] = (byte)(w >> 8); ihdr[3] = (byte)w;
            ihdr[4] = (byte)(h >> 24); ihdr[5] = (byte)(h >> 16); ihdr[6] = (byte)(h >> 8); ihdr[7] = (byte)h;
            ihdr[8] = 8; ihdr[9] = 2;
            Chunk(fs, "IHDR", ihdr);
            using var ms = new MemoryStream();
            using (var z = new ZLibStream(ms, CompressionLevel.Fastest, true))
            {
                var row = new byte[w * 3 + 1];
                for (int y = 0; y < h; y++) { row[0] = 0; Buffer.BlockCopy(rgb, y * w * 3, row, 1, w * 3); z.Write(row, 0, row.Length); }
            }
            Chunk(fs, "IDAT", ms.ToArray());
            Chunk(fs, "IEND", new byte[0]);
        }
    }
}
