using System.Buffers.Binary;

namespace Rahoon.Api.Modules.Market;

/// <summary>
/// Removes embedded metadata from listing photos before they are stored. Phone photos carry EXIF GPS: a published photo
/// would otherwise reveal the exact location of a property shown as «موقع تقريبي». Pixels are not re-encoded.
/// JPEG: APP1 (EXIF/XMP) and APP13 (IPTC) segments · PNG: eXIf, tEXt, iTXt, zTXt chunks · WebP: EXIF and XMP chunks.
/// Unknown or malformed files are returned unchanged (the storage type check already rejected non-images).
/// </summary>
public static class ImageMetadata
{
    public static byte[] Strip(byte[] data, string contentType) => contentType switch
    {
        "image/jpeg" => StripJpeg(data),
        "image/png" => StripPng(data),
        "image/webp" => StripWebp(data),
        _ => data,
    };

    public static string DetectType(ReadOnlySpan<byte> head) => head switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x89, 0x50, 0x4E, 0x47, ..] => "image/png",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        _ => "",
    };

    private static byte[] StripJpeg(byte[] d)
    {
        if (d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8) return d;
        using var o = new MemoryStream(d.Length);
        o.Write(d, 0, 2);
        var i = 2;
        while (i + 4 <= d.Length)
        {
            if (d[i] != 0xFF) return d;
            var marker = d[i + 1];
            if (marker == 0xDA)
            {
                // Start of scan: the compressed image follows; copy the rest unchanged.
                o.Write(d, i, d.Length - i);
                return o.ToArray();
            }
            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7)) { o.Write(d, i, 2); i += 2; continue; }
            var len = (d[i + 2] << 8) | d[i + 3];
            if (len < 2 || i + 2 + len > d.Length) return d;
            if (marker is not (0xE1 or 0xED)) o.Write(d, i, 2 + len);
            i += 2 + len;
        }
        return d;
    }

    private static readonly HashSet<string> PngDrop = ["eXIf", "tEXt", "iTXt", "zTXt"];

    private static byte[] StripPng(byte[] d)
    {
        if (d.Length < 8) return d;
        using var o = new MemoryStream(d.Length);
        o.Write(d, 0, 8);
        var i = 8;
        while (i + 12 <= d.Length)
        {
            var len = (int)BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(i, 4));
            if (len < 0 || i + 12 + len > d.Length) return d;
            var type = System.Text.Encoding.ASCII.GetString(d, i + 4, 4);
            if (!PngDrop.Contains(type)) o.Write(d, i, 12 + len);
            i += 12 + len;
            if (type == "IEND") break;
        }
        return o.ToArray();
    }

    private static byte[] StripWebp(byte[] d)
    {
        if (d.Length < 12) return d;
        using var o = new MemoryStream(d.Length);
        o.Write(d, 0, 12);
        var i = 12;
        while (i + 8 <= d.Length)
        {
            var fourcc = System.Text.Encoding.ASCII.GetString(d, i, 4);
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(i + 4, 4));
            var padded = size + (size & 1);
            if (size < 0 || i + 8 + size > d.Length) return d;
            var take = Math.Min(8 + padded, d.Length - i);
            if (fourcc is not ("EXIF" or "XMP "))
            {
                var start = (int)o.Position;
                o.Write(d, i, take);
                // VP8X flags: clear the EXIF (0x08) and XMP (0x04) bits that announced the removed chunks.
                if (fourcc == "VP8X" && size >= 1)
                {
                    var buf = o.GetBuffer();
                    buf[start + 8] = (byte)(buf[start + 8] & ~0x0C);
                }
            }
            i += take;
        }
        var result = o.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), (uint)(result.Length - 8));
        return result;
    }
}
