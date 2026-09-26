using BenchmarkDotNet.Attributes;
using TerraPDF.Benchmarks.Scenarios;
using TerraPDF.Drawing;

namespace TerraPDF.Benchmarks;

/// <summary>Image decoding and embedding.</summary>
public class ImageBenchmarks : PdfBenchmarkBase
{
    private byte[] _png = [];
    private byte[] _alphaPng = [];
    private byte[] _jpg = [];

    /// <summary>Number of placements of the same image — exposes missing de-duplication.</summary>
    [Params(1, 40)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _png = Assets.HeaderLogoPng();
        _alphaPng = Assets.AlphaBadgePng();
        _jpg = Assets.SmallLogoJpg();
    }

    [Benchmark]
    public int DecodePng()
    {
        using var stream = new MemoryStream(_png, writable: false);
        return PngDecoder.Decode(stream, out _, out _, out _).Length;
    }

    [Benchmark]
    public int DecodeAlphaPng()
    {
        using var stream = new MemoryStream(_alphaPng, writable: false);
        return PngDecoder.Decode(stream, out _, out _, out _).Length;
    }

    [Benchmark]
    public long PngDocument() => Publish(Docs.Images(_png, Count));

    [Benchmark]
    public long AlphaPngDocument() => Publish(Docs.Images(_alphaPng, Count));

    [Benchmark]
    public long JpegDocument() => Publish(Docs.Images(_jpg, Count));
}
