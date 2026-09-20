using System.IO.Compression;
using System.Text;

namespace TerraPDF.Tests;

internal static class TestImageData
{
    internal static byte[] MakeJpegHeaderOnly(int width, int height) =>
    [
        0xFF, 0xD8,
        0xFF, 0xC0, 0x00, 0x11,
        0x08,
        (byte)(height >> 8), (byte)height,
        (byte)(width >> 8), (byte)width,
        0x03,
        0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
        0xFF, 0xD9,
    ];

    internal static byte[] MakePng(int width, int height, bool rgba, byte alphaValue)
    {
        using var stream = new MemoryStream();

        void WriteBigEndian(int value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        void WriteChunk(string type, byte[] data)
        {
            WriteBigEndian(data.Length);
            stream.Write(Encoding.ASCII.GetBytes(type));
            stream.Write(data);
            WriteBigEndian(0); // The test decoder deliberately ignores CRC values.
        }

        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        header[0] = (byte)(width >> 24);
        header[1] = (byte)(width >> 16);
        header[2] = (byte)(width >> 8);
        header[3] = (byte)width;
        header[4] = (byte)(height >> 24);
        header[5] = (byte)(height >> 16);
        header[6] = (byte)(height >> 8);
        header[7] = (byte)height;
        header[8] = 8;
        header[9] = (byte)(rgba ? 6 : 2);
        WriteChunk("IHDR", header);

        int bytesPerPixel = rgba ? 4 : 3;
        var scanlines = new byte[height * (1 + width * bytesPerPixel)];
        int offset = 0;
        for (int y = 0; y < height; y++)
        {
            scanlines[offset++] = 0;
            for (int x = 0; x < width; x++)
            {
                scanlines[offset++] = 200;
                scanlines[offset++] = 100;
                scanlines[offset++] = 50;
                if (rgba)
                    scanlines[offset++] = alphaValue;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(scanlines);
        WriteChunk("IDAT", compressed.ToArray());
        WriteChunk("IEND", []);
        return stream.ToArray();
    }
}
