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
public class BugBmsSpecialBenchmark
{
    private const string DefaultBugBmsPath = @"F:\badbms\ラプラス\bug.bms";

    private byte[] data = Array.Empty<byte>();
    private Encoding encoding = Encoding.UTF8;

    static BugBmsSpecialBenchmark()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Logger.Instance = SilentLogger.Shared;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        encoding = Encoding.GetEncoding("Shift-JIS");

        string fixturePath = Environment.GetEnvironmentVariable("CSBMSPARSER_BUG_BMS") ?? DefaultBugBmsPath;
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                $"Set CSBMSPARSER_BUG_BMS to the local bug.bms path, or place it at '{DefaultBugBmsPath}'.",
                fixturePath);
        }

        data = File.ReadAllBytes(fixturePath);

        BMSModel model = DecodeFixture();
        if (model == null || model.getAllTimeLines().Length == 0)
        {
            throw new InvalidOperationException($"bug.bms did not decode into any timelines: {fixturePath}");
        }
    }

    [Benchmark]
    public int Decode()
    {
        BMSModel model = DecodeFixture();
        return model.getAllTimeLines().Length;
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

[MemoryDiagnoser]
public class BmsR2P1ModelBenchmarks
{
    private byte[] data = Array.Empty<byte>();
    private Encoding encoding = Encoding.UTF8;
    private BMSModel model = new();

    static BmsR2P1ModelBenchmarks()
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
    public double GetMinMaxBpm()
    {
        return model.getMinBPM() + model.getMaxBPM();
    }

    [Benchmark]
    public long GetLastTimes()
    {
        return model.getLastMilliTime()
            + model.getLastNoteMilliTime()
            + model.getLastTime()
            + model.getLastNoteTime();
    }

    [Benchmark]
    public int ContainsFlags()
    {
        int flags = 0;
        flags |= model.containsUndefinedLongNote() ? 1 : 0;
        flags |= model.containsLongNote() ? 2 : 0;
        flags |= model.containsMineNote() ? 4 : 0;
        return flags;
    }

    [Benchmark]
    public long BuildAllTimes()
    {
        long[] times = model.getAllTimes();
        if (times.Length == 0)
        {
            return 0;
        }

        return times.Length + times[0] + times[times.Length - 1];
    }

    [Benchmark]
    public int TotalNotesAllAndTyped()
    {
        return model.getTotalNotes()
            + BMSModelUtils.getTotalNotes(model, BMSModelUtils.TOTALNOTES_KEY)
            + BMSModelUtils.getTotalNotes(model, BMSModelUtils.TOTALNOTES_LONG_KEY)
            + BMSModelUtils.getTotalNotes(model, BMSModelUtils.TOTALNOTES_SCRATCH)
            + BMSModelUtils.getTotalNotes(model, BMSModelUtils.TOTALNOTES_LONG_SCRATCH)
            + BMSModelUtils.getTotalNotes(model, BMSModelUtils.TOTALNOTES_MINE);
    }

    [Benchmark]
    public int TotalNotesWindow()
    {
        int last = model.getLastTime();
        int midpoint = last > 0 ? last / 2 : int.MaxValue;
        return BMSModelUtils.getTotalNotes(model, 0, midpoint, BMSModelUtils.TOTALNOTES_ALL)
            + BMSModelUtils.getTotalNotes(model, midpoint, int.MaxValue, BMSModelUtils.TOTALNOTES_ALL);
    }

    private BMSModel DecodeFixture()
    {
        var decoder = new BMSDecoder();
        return decoder.decode(data, ispms: false, random: null, encoding);
    }
}

[MemoryDiagnoser]
public class BmsR2P0DecodeBenchmarks
{
    private const int RandomBlocks = 360;

    private byte[] data = Array.Empty<byte>();
    private Encoding encoding = Encoding.UTF8;
    private int[] selectedRandoms;

    static BmsR2P0DecodeBenchmarks()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Logger.Instance = SilentLogger.Shared;
    }

    [Params(R2P0FixtureKind.HeaderStress, R2P0FixtureKind.RandomBranch, R2P0FixtureKind.DenseLnObj, R2P0FixtureKind.DenseLnChannel)]
    public R2P0FixtureKind FixtureKind { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        encoding = Encoding.GetEncoding("Shift-JIS");
        selectedRandoms = null;

        string source = FixtureKind switch
        {
            R2P0FixtureKind.HeaderStress => CreateHeaderStressFixture(),
            R2P0FixtureKind.RandomBranch => CreateRandomBranchFixture(out selectedRandoms),
            R2P0FixtureKind.DenseLnObj => CreateDenseLnObjFixture(),
            R2P0FixtureKind.DenseLnChannel => CreateDenseLnChannelFixture(),
            _ => throw new InvalidOperationException($"Unknown fixture kind: {FixtureKind}")
        };

        data = encoding.GetBytes(source);

        BMSModel model = DecodeFixture();
        if (model == null || model.getAllTimeLines().Length == 0)
        {
            throw new InvalidOperationException($"R2-P0 fixture '{FixtureKind}' did not decode into any timelines.");
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
        return decoder.decode(data, ispms: false, random: selectedRandoms, encoding);
    }

    private static string CreateHeaderStressFixture()
    {
        var sb = CreateBaseFixture("R2P0 Header Stress");

        for (int i = 1; i <= 720; i++)
        {
            string key = ToBase36(i);
            sb.Append("#WAV").Append(key).Append(" sample_").Append(i).Append(".wav").AppendLine();
        }

        for (int i = 1; i <= 240; i++)
        {
            string key = ToBase36(i);
            sb.Append("#BMP").Append(key).Append(" image_").Append(i).Append(".png").AppendLine();
        }

        for (int i = 1; i <= 180; i++)
        {
            string key = ToBase36(i);
            sb.Append("#BPM").Append(key).Append(' ').Append(100 + i % 80).AppendLine();
            sb.Append("#STOP").Append(key).Append(' ').Append(12 + i % 96).AppendLine();
            sb.Append("#SCROLL").Append(key).Append(' ').Append(i % 2 == 0 ? "1.25" : "0.75").AppendLine();
        }

        for (int section = 1; section <= 120; section++)
        {
            AppendChartLine(sb, section, "11", RepeatPair("01", 16));
            AppendChartLine(sb, section, "08", RepeatPair(ToBase36(section % 180 + 1), 4));
            AppendChartLine(sb, section, "09", RepeatPair(ToBase36(section % 180 + 1), 4));
            AppendChartLine(sb, section, "SC", RepeatPair(ToBase36(section % 180 + 1), 4));
        }

        return sb.ToString();
    }

    private static string CreateRandomBranchFixture(out int[] selectedRandoms)
    {
        selectedRandoms = new int[RandomBlocks];
        Array.Fill(selectedRandoms, 2);

        var sb = CreateBaseFixture("R2P0 Random Branch");
        sb.AppendLine("#WAV01 branch_a.wav");
        sb.AppendLine("#WAV02 branch_b.wav");

        for (int i = 1; i <= RandomBlocks; i++)
        {
            sb.AppendLine("#RANDOM 2");
            sb.AppendLine("#IF 1");
            AppendChartLine(sb, i, "11", RepeatPair("01", 4));
            sb.AppendLine("#ENDIF");
            sb.AppendLine("#IF 2");
            AppendChartLine(sb, i, "12", RepeatPair("02", 4));
            sb.AppendLine("#ENDIF");
            sb.AppendLine("#ENDRANDOM");
        }

        return sb.ToString();
    }

    private static string CreateDenseLnObjFixture()
    {
        var sb = CreateBaseFixture("R2P0 Dense LNOBJ");
        sb.AppendLine("#LNOBJ ZZ");
        sb.AppendLine("#WAV01 note.wav");
        sb.AppendLine("#WAVZZ ln_end.wav");

        string data = RepeatPair("01ZZ", 12);
        for (int section = 1; section <= 220; section++)
        {
            AppendChartLine(sb, section, "11", data);
            AppendChartLine(sb, section, "12", data);
        }

        return sb.ToString();
    }

    private static string CreateDenseLnChannelFixture()
    {
        var sb = CreateBaseFixture("R2P0 Dense LN Channel");
        sb.AppendLine("#WAV01 ln.wav");

        string longData = RepeatPair("0101", 12);
        string mineData = RepeatPair("00010001", 6);
        for (int section = 1; section <= 220; section++)
        {
            AppendChartLine(sb, section, "51", longData);
            AppendChartLine(sb, section, "52", longData);
            AppendChartLine(sb, section, "D1", mineData);
            AppendChartLine(sb, section, "D2", mineData);
        }

        return sb.ToString();
    }

    private static StringBuilder CreateBaseFixture(string title)
    {
        return new StringBuilder()
            .AppendLine("#PLAYER 1")
            .AppendLine("#GENRE Benchmark")
            .Append("#TITLE ").Append(title).AppendLine()
            .AppendLine("#ARTIST CSBMSParser")
            .AppendLine("#PLAYLEVEL 1")
            .AppendLine("#RANK 2")
            .AppendLine("#TOTAL 100")
            .AppendLine("#BPM 120");
    }

    private static void AppendChartLine(StringBuilder sb, int section, string channel, string data)
    {
        sb.Append('#').Append(section.ToString("000")).Append(channel).Append(':').Append(data).AppendLine();
    }

    private static string RepeatPair(string value, int count)
    {
        var sb = new StringBuilder(value.Length * count);
        for (int i = 0; i < count; i++)
        {
            sb.Append(value);
        }

        return sb.ToString();
    }

    private static string ToBase36(int value)
    {
        const string digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return string.Create(2, value, static (span, state) =>
        {
            span[0] = digits[state / 36];
            span[1] = digits[state % 36];
        });
    }
}

public enum R2P0FixtureKind
{
    HeaderStress,
    RandomBranch,
    DenseLnObj,
    DenseLnChannel
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
