using System.IO.Compression;
using System.Text;

namespace Rahoon.Api.Seed;

/// <summary>
/// Placeholder listing photos for the demo data: an abstract building illustration drawn in code (no photograph of any real
/// property). The UI labels demo opportunities «تجريبي», so these are never mistaken for the real thing.
/// </summary>
public static class DemoImages
{
    private static readonly (byte R, byte G, byte B)[][] Palettes =
    [
        [(250, 249, 246), (240, 184, 166), (170, 69, 40), (34, 38, 42), (213, 189, 141)],
        [(234, 242, 249), (157, 192, 222), (29, 90, 140), (34, 38, 42), (242, 241, 237)],
        [(234, 244, 238), (156, 203, 176), (30, 106, 69), (34, 38, 42), (251, 242, 222)],
        [(251, 242, 222), (226, 194, 122), (138, 83, 0), (34, 38, 42), (250, 249, 246)],
    ];

    /// <summary>PNG bytes of a w×h illustration; <paramref name="variant"/> picks colours and the building shape.</summary>
    public static byte[] Building(int variant, int w = 960, int h = 640)
    {
        var p = Palettes[variant % Palettes.Length];
        var sky = p[0]; var light = p[1]; var accent = p[2]; var ink = p[3]; var ground = p[4];
        var px = new byte[w * h * 3];
        void Set(int x, int y, (byte R, byte G, byte B) c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            var i = (y * w + x) * 3;
            px[i] = c.R; px[i + 1] = c.G; px[i + 2] = c.B;
        }
        void Rect(int x0, int y0, int x1, int y1, (byte, byte, byte) c)
        {
            for (var y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
                for (var x = Math.Max(0, x0); x < Math.Min(w, x1); x++) Set(x, y, c);
        }
        // Sky gradient towards the light colour, then the ground band.
        for (var y = 0; y < h; y++)
        {
            var t = y / (double)h;
            var c = ((byte)(sky.R + (light.R - sky.R) * t * 0.6), (byte)(sky.G + (light.G - sky.G) * t * 0.6), (byte)(sky.B + (light.B - sky.B) * t * 0.6));
            for (var x = 0; x < w; x++) Set(x, y, c);
        }
        var groundY = (int)(h * 0.82);
        Rect(0, groundY, w, h, ground);

        var kind = variant % 3;
        if (kind == 0)
        {
            // Apartment tower with a window grid.
            int x0 = w / 2 - 170, x1 = w / 2 + 170, y0 = (int)(h * 0.16);
            Rect(x0, y0, x1, groundY, accent);
            Rect(x0 - 12, y0 - 14, x1 + 12, y0, ink);
            for (var row = 0; row < 9; row++)
                for (var col = 0; col < 5; col++)
                {
                    var wx = x0 + 22 + col * 64; var wy = y0 + 22 + row * 50;
                    if (wy + 30 < groundY - 10) Rect(wx, wy, wx + 40, wy + 30, (row + col + variant) % 4 == 0 ? light : sky);
                }
            Rect(w / 2 - 30, groundY - 70, w / 2 + 30, groundY, ink);
        }
        else if (kind == 1)
        {
            // Villa: two volumes, flat roofs, wide windows.
            int baseY = groundY;
            Rect(w / 2 - 300, baseY - 230, w / 2 + 40, baseY, accent);
            Rect(w / 2 + 40, baseY - 160, w / 2 + 300, baseY, light);
            Rect(w / 2 - 320, baseY - 246, w / 2 + 60, baseY - 230, ink);
            Rect(w / 2 + 30, baseY - 176, w / 2 + 320, baseY - 160, ink);
            Rect(w / 2 - 260, baseY - 190, w / 2 - 120, baseY - 120, sky);
            Rect(w / 2 - 90, baseY - 190, w / 2 + 10, baseY - 120, sky);
            Rect(w / 2 + 90, baseY - 120, w / 2 + 250, baseY - 60, sky);
            Rect(w / 2 - 60, baseY - 90, w / 2 - 10, baseY, ink);
            Rect(0, baseY - 40, w / 2 - 330, baseY, ground);
        }
        else
        {
            // Row of townhouses.
            for (var k = 0; k < 4; k++)
            {
                int x0 = 80 + k * 205, x1 = x0 + 190, top = groundY - 210 - (k % 2) * 30;
                Rect(x0, top, x1, groundY, k % 2 == 0 ? accent : light);
                Rect(x0 - 6, top - 12, x1 + 6, top, ink);
                Rect(x0 + 25, top + 30, x0 + 85, top + 90, sky);
                Rect(x0 + 105, top + 30, x0 + 165, top + 90, sky);
                Rect(x0 + 75, groundY - 70, x0 + 115, groundY, ink);
            }
        }
        return EncodePng(px, w, h);
    }

    /// <summary>A minimal valid one-page PDF for demo private documents.</summary>
    public static byte[] Pdf(string title) =>
        Encoding.ASCII.GetBytes($"%PDF-1.4\n% Rahoon demo document (fictional): {new string(title.Where(c => c < 128).ToArray())}\n1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n");

    private static byte[] EncodePng(byte[] rgb, int w, int h)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < h; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgb, y * w * 3, w * 3);
        }
        using var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) raw.WriteTo(zs);

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        WriteBe(ihdr, 0, (uint)w); WriteBe(ihdr, 4, (uint)h);
        ihdr[8] = 8; ihdr[9] = 2; // 8-bit RGB
        Chunk(png, "IHDR", ihdr);
        Chunk(png, "IDAT", z.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBe(len, 0, (uint)data.Length);
        s.Write(len);
        var td = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        s.Write(td);
        var crc = new byte[4];
        WriteBe(crc, 0, Crc32(td));
        s.Write(crc);
    }

    private static void WriteBe(byte[] b, int o, uint v)
    {
        b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc32(byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
