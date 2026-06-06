using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace CSBMSParser.Benchmark;

public static class Program
{
    public static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Logger.Instance = SilentLogger.Shared;
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}

[MemoryDiagnoser]
public class BmsDecodeBenchmarks
{
    private byte[] data = Array.Empty<byte>();
    private Encoding encoding = Encoding.UTF8;

    static BmsDecodeBenchmarks()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Logger.Instance = SilentLogger.Shared;
    }

    [Params("Lapras_A.bml", "AF_7KSHD.bms", "_ANOTHER.bms", "tetrinet_5.bms")]
    public string FixtureName { get; set; } = "Lapras_A.bml";

    [GlobalSetup]
    public void GlobalSetup()
    {
        encoding = Encoding.GetEncoding("Shift-JIS");

        string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", FixtureName);
        data = File.ReadAllBytes(fixturePath);

        BMSModel model = DecodeFixture();
        if (model == null || model.getAllTimeLines().Length == 0)
        {
            throw new InvalidOperationException($"Fixture '{FixtureName}' did not decode into any timelines.");
        }
    }

    [Benchmark]
    public int Decode()
    {
        return DecodeFixture().getAllTimeLines().Length;
    }

    private BMSModel DecodeFixture()
    {
        var decoder = new BMSDecoder();
        return decoder.decode(data, ispms: false, random: null, encoding);
    }
}

[MemoryDiagnoser]
public class BmsP1ModelBenchmarks
{
    private byte[] data = Array.Empty<byte>();
    private Encoding encoding = Encoding.UTF8;
    private BMSModel model = new();

    static BmsP1ModelBenchmarks()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Logger.Instance = SilentLogger.Shared;
    }

    [Params("Lapras_A.bml", "AF_7KSHD.bms", "_ANOTHER.bms", "tetrinet_5.bms")]
    public string FixtureName { get; set; } = "Lapras_A.bml";

    [GlobalSetup]
    public void GlobalSetup()
    {
        encoding = Encoding.GetEncoding("Shift-JIS");

        string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", FixtureName);
        data = File.ReadAllBytes(fixturePath);
        model = DecodeFixture();

        if (model == null || model.getAllTimeLines().Length == 0)
        {
            throw new InvalidOperationException($"Fixture '{FixtureName}' did not decode into any timelines.");
        }
    }

    [Benchmark]
    public double MaxNotesPerSecond()
    {
        return BMSModelUtils.getMaxNotesPerTime(model, 1000);
    }

    [Benchmark]
    public int BuildLanes()
    {
        Lane[] lanes = model.getLanes();
        int count = 0;
        foreach (Lane lane in lanes)
        {
            count += lane.getNotes().Length;
            count += lane.getHiddens().Length;
        }
        return count;
    }

    [Benchmark]
    public int BuildEventLane()
    {
        EventLane eventLane = model.getEventLane();
        return eventLane.getSections().Length + eventLane.getBpmChanges().Length + eventLane.getStops().Length;
    }

    private BMSModel DecodeFixture()
    {
        var decoder = new BMSDecoder();
        return decoder.decode(data, ispms: false, random: null, encoding);
    }
}

internal sealed class SilentLogger : Logger
{
    public static readonly SilentLogger Shared = new();

    public override void fine(string str)
    {
    }

    public override void severe(string v)
    {
    }

    public override void warning(string v)
    {
    }
}
