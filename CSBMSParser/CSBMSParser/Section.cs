using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static CSBMSParser.ChartDecoder;

namespace CSBMSParser
{
    /**
 * 小節
 * 
 * @author exch
 */
    public class Section
    {
        private const int DenseSectionObjectThreshold = 16 * 1024;

        public const int ILLEGAL = -1;
        public const int LANE_AUTOPLAY = 1;
        public const int SECTION_RATE = 2;
        public const int BPM_CHANGE = 3;
        public const int BGA_PLAY = 4;
        public const int POOR_PLAY = 6;
        public const int LAYER_PLAY = 7;
        public const int BPM_CHANGE_EXTEND = 8;
        public const int STOP = 9;

        public const int P1_KEY_BASE = 1 * 36 + 1;
        public const int P2_KEY_BASE = 2 * 36 + 1;
        public const int P1_INVISIBLE_KEY_BASE = 3 * 36 + 1;
        public const int P2_INVISIBLE_KEY_BASE = 4 * 36 + 1;
        public const int P1_LONG_KEY_BASE = 5 * 36 + 1;
        public const int P2_LONG_KEY_BASE = 6 * 36 + 1;
        public const int P1_MINE_KEY_BASE = 13 * 36 + 1;
        public const int P2_MINE_KEY_BASE = 14 * 36 + 1;

        public const int SCROLL = 1020;

        public readonly static int[] NOTE_CHANNELS = new[]{P1_KEY_BASE, P2_KEY_BASE ,P1_INVISIBLE_KEY_BASE, P2_INVISIBLE_KEY_BASE,
            P1_LONG_KEY_BASE, P2_LONG_KEY_BASE, P1_MINE_KEY_BASE, P2_MINE_KEY_BASE};

        /**
		 * 小節の拡大倍率
		 */
        private double rate = 1.0;
        /**
		 * POORアニメーション
		 */
        private int[] poor = new int[0];

        private BMSModel model;

        private double sectionnum;

        private List<DecodeLog> log;

        private List<string> channellines;

        public Section(BMSModel model, Section prev, List<string> lines, Dictionary<int, double> bpmtable,
                Dictionary<int, double> stoptable, Dictionary<int, double> scrolltable, List<DecodeLog> log)
        {
            this.model = model;
            this.log = log;

            channellines = new List<string>(lines.Count);
            if (prev != null)
            {
                sectionnum = prev.sectionnum + prev.rate;
            }
            else
            {
                sectionnum = 0;
            }
            foreach (string line in lines)
            {
                int channel = ChartDecoder.parseInt36(line[4], line[5]);
                switch (channel)
                {
                    case ILLEGAL:
                        log.Add(new DecodeLog(DecodeLog.State.WARNING, "チャンネル定義が無効です : " + line));
                        break;
                    // BGレーン
                    case LANE_AUTOPLAY:
                    // BGAレーン
                    case BGA_PLAY:
                    // レイヤー
                    case LAYER_PLAY:
                        channellines.Add(line);
                        break;
                    // 小節の拡大率
                    case SECTION_RATE:
                        int colon_index = line.IndexOf(":");
                        try
                        {
                            rate = double.Parse(line.substring(colon_index + 1, line.Length));
                        }
                        catch (Exception)
                        {
                            log.Add(new DecodeLog(DecodeLog.State.WARNING, "小節の拡大率が不正です : " + line));
                        }
                        break;
                    // BPM変化
                    case BPM_CHANGE:
                        this.processData(line, (pos, data) =>
                        {
                            bpmchange[pos] = data;
                        }, null, null, ParseHexPair);
                        break;
                    // POORアニメーション
                    case POOR_PLAY:
                        poor = this.splitData(line);
                        // アニメーションが単一画像のみの定義の場合、0を除外する(ミスレイヤーチャンネルの定義が曖昧)
                        int singleid = 0;
                        foreach (int id in poor)
                        {
                            if (id != 0)
                            {
                                if (singleid != 0 && singleid != id)
                                {
                                    singleid = -1;
                                    break;
                                }
                                else
                                {
                                    singleid = id;
                                }
                            }
                        }
                        if (singleid != -1)
                        {
                            poor = new int[] { singleid };
                        }
                        break;
                    // BPM変化(拡張)
                    case BPM_CHANGE_EXTEND:
                        this.processData(line, (pos, data) =>
                        {
                            if (bpmtable.TryGetValue(data, out var bpm))
                            {
                                bpmchange[pos] = bpm;
                            }
                            else
                            {
                                log.Add(new DecodeLog(DecodeLog.State.WARNING, "未定義のBPM変化を参照しています : " + data));
                            }
                        });
                        break;
                    // ストップシーケンス
                    case STOP:
                        this.processData(line, (pos, data) =>
                        {
                            if (stoptable.TryGetValue(data,out var st))
                            {
                                stop[pos] = st;
                            }
                            else
                            {
                                log.Add(new DecodeLog(DecodeLog.State.WARNING, "未定義のSTOPを参照しています : " + data));
                            }
                        });
                        break;
                    // scroll
                    case SCROLL:
                        this.processData(line, (pos, data) =>
                        {
                            if (scrolltable.TryGetValue(data, out var st))
                            {
                                scroll[pos] = st;
                            }
                            else
                            {
                                log.Add(new DecodeLog(DecodeLog.State.WARNING, "未定義のSCROLLを参照しています : " + data));
                            }
                        });
                        break;
                }

                int basech = 0;
                int ch2 = -1;
                foreach (int ch in NOTE_CHANNELS)
                {
                    if (ch <= channel && channel <= ch + 8)
                    {
                        basech = ch;
                        ch2 = channel - ch;
                        channellines.Add(line);
                        break;
                    }
                }
                // 5/10KEY -> 7/14KEY
                if (ch2 == 7 || ch2 == 8)
                {
                    var mode = (model.getMode() == Mode.BEAT_5K) ? Mode.BEAT_7K : (model.getMode() == Mode.BEAT_10K ? Mode.BEAT_14K : null);
                    if (mode != null && HasAnyPositiveData(line))
                    {
                        model.setMode(mode);
                    }
                }
                // 5/7KEY -> 10/14KEY			
                if (basech == P2_KEY_BASE || basech == P2_INVISIBLE_KEY_BASE || basech == P2_LONG_KEY_BASE || basech == P2_MINE_KEY_BASE)
                {
                    var mode = (model.getMode() == Mode.BEAT_5K) ? Mode.BEAT_10K : (model.getMode() == Mode.BEAT_7K ? Mode.BEAT_14K : null);
                    if (mode != null && HasAnyPositiveData(line))
                    {
                        model.setMode(mode);
                    }
                }
            }
        }

        private int[] splitData(string line)
        {
            var findex = GetDataStart(line);
            if (findex < 0)
            {
                log.Add(new DecodeLog(DecodeLog.State.WARNING, "チャンネル定義形式が無効です : " + SummarizeLine(line)));
                return Array.Empty<int>();
            }

            var lindex = line.Length;
            var dataLength = lindex - findex;
            var split = dataLength / 2;
            int[] result = new int[split];
            int invalid = 0;
            for (int i = 0; i < split; i++)
            {
                result[i] = ParseBase36Pair(line[(findex + i * 2)], line[(findex + i * 2 + 1)]);
                if (result[i] == -1)
                {
                    invalid++;
                    result[i] = 0;
                }
            }
            if (invalid > 0)
            {
                AddChannelDataLog("チャンネル定義中の不正な値をスキップしました", invalid, line);
            }
            if ((dataLength & 1) != 0)
            {
                AddChannelDataLog("チャンネル定義中の孤立した末尾文字をスキップしました", 1, line);
            }
            return result;
        }

        private void processData(string line, DataProcessor processor)
        {
            processData(line, processor, null, null, ParseBase36Pair);
        }

        private void processData(string line, DataProcessor processor, Func<int, bool> shouldProcess, string skippedMessage)
        {
            processData(line, processor, shouldProcess, skippedMessage, ParseBase36Pair);
        }

        private void processData(string line, DataProcessor processor, Func<int, bool> shouldProcess, string skippedMessage, ObjectParser parser)
        {
            var findex = GetDataStart(line);
            if (findex < 0)
            {
                log.Add(new DecodeLog(DecodeLog.State.WARNING, "チャンネル定義形式が無効です : " + SummarizeLine(line)));
                return;
            }

            var lindex = line.Length;
            var dataLength = lindex - findex;
            var split = dataLength / 2;
            int invalid = 0;
            int skipped = 0;
            for (int i = 0; i < split; i++)
            {
                int result = parser(line[(findex + i * 2)], line[(findex + i * 2 + 1)]);
                if (result > 0)
                {
                    if (shouldProcess == null || shouldProcess(result))
                    {
                        processor((double)i / split, result);
                    }
                    else
                    {
                        skipped++;
                    }
                }
                else if (result == -1)
                {
                    invalid++;
                }
            }
            if (invalid > 0)
            {
                AddChannelDataLog("チャンネル定義中の不正な値をスキップしました", invalid, line);
            }
            if ((dataLength & 1) != 0)
            {
                AddChannelDataLog("チャンネル定義中の孤立した末尾文字をスキップしました", 1, line);
            }
            if (skipped > 0 && skippedMessage != null)
            {
                AddChannelDataLog(skippedMessage, skipped, line);
            }
        }

        public delegate void DataProcessor(double pos, int data);
        private delegate int ObjectParser(char high, char low);

        private static int GetDataStart(string line)
        {
            int colon = line.IndexOf(':');
            return colon >= 0 ? colon + 1 : -1;
        }

        private static int GetPairCount(string line)
        {
            int dataStart = GetDataStart(line);
            return dataStart >= 0 ? (line.Length - dataStart) / 2 : 0;
        }

        private static int ParseBase36Pair(char high, char low)
        {
            return ChartDecoder.parseInt36(high, low);
        }

        private static int ParseHexPair(char high, char low)
        {
            int highValue = ParseHexDigit(high);
            int lowValue = ParseHexDigit(low);
            return highValue >= 0 && lowValue >= 0 ? highValue * 16 + lowValue : -1;
        }

        private static int ParseHexDigit(char value)
        {
            if (value >= '0' && value <= '9')
            {
                return value - '0';
            }
            if (value >= 'a' && value <= 'f')
            {
                return value - 'a' + 10;
            }
            if (value >= 'A' && value <= 'F')
            {
                return value - 'A' + 10;
            }
            return -1;
        }

        private static bool HasAnyPositiveData(string line)
        {
            int dataStart = GetDataStart(line);
            if (dataStart < 0)
            {
                return false;
            }

            int split = (line.Length - dataStart) / 2;
            for (int i = 0; i < split; i++)
            {
                if (ParseBase36Pair(line[dataStart + i * 2], line[dataStart + i * 2 + 1]) > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void AddChannelDataLog(string message, int count, string line)
        {
            log.Add(new DecodeLog(DecodeLog.State.WARNING,
                model.getTitle() + ":" + message + " count=" + count + " : " + SummarizeLine(line)));
        }

        private static string SummarizeLine(string line)
        {
            const int prefixLength = 96;
            if (line.Length <= prefixLength)
            {
                return line;
            }

            return line.Substring(0, prefixLength) + "... (len=" + line.Length + ")";
        }

        private SortedDictionary<double, double> bpmchange = new();
        private SortedDictionary<double, double> stop = new();
        private SortedDictionary<double, double> scroll = new();

        private readonly static int[] CHANNELASSIGN_BEAT5 = { 0, 1, 2, 3, 4, 5, -1, -1, -1, 6, 7, 8, 9, 10, 11, -1, -1, -1 };
        private readonly static int[] CHANNELASSIGN_BEAT7 = { 0, 1, 2, 3, 4, 7, -1, 5, 6, 8, 9, 10, 11, 12, 15, -1, 13, 14 };
        private readonly static int[] CHANNELASSIGN_POPN = { 0, 1, 2, 3, 4, -1, -1, -1, -1, -1, 5, 6, 7, 8, -1, -1, -1, -1 };

        private SortedList<double, TimeLineCache> tlcache;

        /**
         * SectionモデルからTimeLineモデルを作成し、BMSModelに登録する
         */
        public void makeTimeLines(int[] wavmap, int[] bgamap, SortedList<double, TimeLineCache> tlcache, List<LongNote>[] lnlist, LongNote[] startln)
        {
            var lnobj = model.getLnobj();
            var lnmode = model.getLnmode();
            this.tlcache = tlcache;
            var cassign = model.getMode() == Mode.POPN_9K ? CHANNELASSIGN_POPN :
                (model.getMode() == Mode.BEAT_7K || model.getMode() == Mode.BEAT_14K ? CHANNELASSIGN_BEAT7 : CHANNELASSIGN_BEAT5);
            // 小節線追加
            var basetl = getTimeLine(sectionnum);
            basetl.setSectionLine(true);

            if (poor.Length > 0)
            {
                var poors = new Layer.Sequence[poor.Length + 1];
                var poortime = 500;

                for (int i = 0; i < poor.Length; i++)
                {
                    if (bgamap[poor[i]] != -2)
                    {
                        poors[i] = new Layer.Sequence((long)(i * poortime / poor.Length), bgamap[poor[i]]);
                    }
                    else
                    {
                        poors[i] = new Layer.Sequence((long)(i * poortime / poor.Length), -1);
                    }
                }
                poors[poors.Length - 1] = new Layer.Sequence(poortime);
                basetl.setEventlayer(new Layer[] { new Layer(new Layer.Event(Layer.EventType.MISS, 1), new Layer.Sequence[][] { poors }) });
            }
            // BPM変化。ストップシーケンステーブル準備
            var stops = stop.GetEnumerator();
            KeyValuePair<double, double>? ste = stops.MoveNext() ? stops.Current : null as KeyValuePair<double, double>?;
            var bpms = bpmchange.GetEnumerator();
            KeyValuePair<double, double>? bce = bpms.MoveNext() ? bpms.Current : null as KeyValuePair<double, double>?;
            var scrolls = scroll.GetEnumerator();
            KeyValuePair<double, double>? sce = scrolls.MoveNext() ? scrolls.Current : null as KeyValuePair<double, double>?;

            while (ste != null || bce != null || sce != null)
            {
                var bc = bce != null ? bce?.Key : 2;
                var st = ste != null ? ste?.Key : 2;
                var sc = sce != null ? sce?.Key : 2;
                if (sc <= st && sc <= bc)
                {
                    getTimeLine(sectionnum + (sc ?? default) * rate).setScroll(sce?.Value ?? default);
                    sce = scrolls.MoveNext() ? scrolls.Current : null as KeyValuePair<double, double>?;
                }
                else if (bc <= st)
                {
                    getTimeLine(sectionnum + (bc ?? default) * rate).setBPM(bce?.Value ?? default);
                    bce = bpms.MoveNext() ? bpms.Current : null as KeyValuePair<double, double>?;
                }
                else if (st <= 1)
                {
                    var tl = getTimeLine(sectionnum + (ste?.Key ?? default) * rate);
                    tl.setStop((long)(1000.0 * 1000 * 60 * 4 * ste?.Value / (tl.getBPM())));
                    ste = stops.MoveNext() ? stops.Current : null as KeyValuePair<double, double>?;
                }
            }

            if (ShouldPrecreateDenseTimelines())
            {
                PrecreateDenseTimelines(wavmap, bgamap, cassign, lnobj);
            }

            foreach (string line in channellines)
            {
                int channel = ChartDecoder.parseInt36(line[(4)], line[(5)]);
                int tmpkey = 0;
                if (channel >= P1_KEY_BASE && channel < P1_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P1_KEY_BASE];
                    channel = P1_KEY_BASE;
                }
                else if (channel >= P2_KEY_BASE && channel < P2_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P2_KEY_BASE + 9];
                    channel = P1_KEY_BASE;
                }
                else if (channel >= P1_INVISIBLE_KEY_BASE && channel < P1_INVISIBLE_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P1_INVISIBLE_KEY_BASE];
                    channel = P1_INVISIBLE_KEY_BASE;
                }
                else if (channel >= P2_INVISIBLE_KEY_BASE && channel < P2_INVISIBLE_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P2_INVISIBLE_KEY_BASE + 9];
                    channel = P1_INVISIBLE_KEY_BASE;
                }
                else if (channel >= P1_LONG_KEY_BASE && channel < P1_LONG_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P1_LONG_KEY_BASE];
                    channel = P1_LONG_KEY_BASE;
                }
                else if (channel >= P2_LONG_KEY_BASE && channel < P2_LONG_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P2_LONG_KEY_BASE + 9];
                    channel = P1_LONG_KEY_BASE;
                }
                else if (channel >= P1_MINE_KEY_BASE && channel < P1_MINE_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P1_MINE_KEY_BASE];
                    channel = P1_MINE_KEY_BASE;
                }
                else if (channel >= P2_MINE_KEY_BASE && channel < P2_MINE_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P2_MINE_KEY_BASE + 9];
                    channel = P1_MINE_KEY_BASE;
                }
                var key = tmpkey;
                if (key == -1)
                {
                    continue;
                }
                switch (channel)
                {
                    case P1_KEY_BASE:
                        this.processData(line, (DataProcessor)((pos, data) =>
                        {
                            // normal note, lnobj
                            var tl = getTimeLine(sectionnum + rate * pos);
                            if (tl.existNote(key))
                            {
                                log.Add(new DecodeLog(DecodeLog.State.WARNING, (string)("通常ノート追加時に衝突が発生しました : " + (key + 1) + ":" + tl.getMilliTime())));
                            }
                            if (data == lnobj)
                            {
                                // LN終端処理
                                // TODO 高速化のために直前のノートを記録しておく
                                foreach (var e in tlcache.descendingMap())
                                {
                                    if (e.Key >= tl.getSection())
                                    {
                                        continue;
                                    }
                                    var tl2 = e.Value.timeline;
                                    if (tl2.existNote(key))
                                    {
                                        var note = tl2.getNote(key);
                                        if (note is NormalNote)
                                        {
                                            // LNOBJの直前のノートをLNに差し替える
                                            var ln = new LongNote(note.getWav());
                                            ln.setType(lnmode);
                                            tl2.setNote(key, ln);
                                            LongNote lnend = new LongNote(-2);
                                            tl.setNote(key, lnend);
                                            ln.setPair(lnend);

                                            if (lnlist[key] == null)
                                            {
                                                lnlist[key] = new List<LongNote>();
                                            }
                                            lnlist[key].Add(ln);
                                            break;
                                        }
                                        else if (note is LongNote && ((LongNote)note).getPair() == null)
                                        {
                                            log.Add(new DecodeLog(DecodeLog.State.WARNING,
                                                    "LNレーンで開始定義し、LNオブジェクトで終端定義しています。レーン: " + (key + 1) + " - Section : "
                                                            + tl2.getSection() + " - " + tl.getSection()));
                                            LongNote lnend = new LongNote(-2);
                                            tl.setNote(key, lnend);
                                            ((LongNote)note).setPair(lnend);

                                            if (lnlist[key] == null)
                                            {
                                                lnlist[key] = new List<LongNote>();
                                            }
                                            lnlist[key].Add((LongNote)note);
                                            startln[key] = null;
                                            break;
                                        }
                                        else
                                        {
                                            log.Add(new DecodeLog(DecodeLog.State.WARNING, (string)("LNオブジェクトの対応が取れません。レーン: " + key
                                                    + " - Time(ms):" + tl2.getMilliTime())));
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                tl.setNote(key, new NormalNote(wavmap[data]));
                            }
                        }), data => data == lnobj || IsDefinedSound(wavmap, data), "未定義または空のWAV参照をスキップしました");
                        break;
                    case P1_INVISIBLE_KEY_BASE:
                        this.processData(line, (pos, data) =>
                        {
                            getTimeLine(sectionnum + rate * pos).setHiddenNote(key, new NormalNote(wavmap[data]));
                        }, data => IsDefinedSound(wavmap, data), "未定義または空のWAV参照をスキップしました");
                        break;
                    case P1_LONG_KEY_BASE:
                        this.processData(line, (DataProcessor)((pos, data) =>
                        {
                            // long note
                            var tl = getTimeLine(sectionnum + rate * pos);
                            var insideln = false;
                            if (!insideln && lnlist[key] != null)
                            {
                                var section = tl.getSection();
                                foreach (LongNote ln in lnlist[key])
                                {
                                    if (ln.getSection() <= section && section <= ln.getPair().getSection())
                                    {
                                        insideln = true;
                                        break;
                                    }
                                }
                            }

                            if (!insideln)
                            {
                                // LN処理
                                if (startln[key] == null)
                                {
                                    if (tl.existNote(key))
                                    {
                                        Note note = tl.getNote(key);
                                        log.Add(new DecodeLog(DecodeLog.State.WARNING, (string)("LN開始位置に通常ノートが存在します。レーン: "
                                                + (key + 1) + " - Time(ms):" + tl.getMilliTime())));
                                        if (note is NormalNote && note.getWav() != wavmap[data])
                                        {
                                            tl.addBackGroundNote(note);
                                        }
                                    }
                                    LongNote ln = new LongNote(wavmap[data]);
                                    tl.setNote(key, ln);
                                    startln[key] = ln;
                                }
                                else if (startln[key].getSection() == double.MinValue)
                                {
                                    startln[key] = null;
                                }
                                else
                                {
                                    // LN終端処理
                                    foreach (var e in tlcache.descendingMap())
                                    {
                                        if (e.Key >= tl.getSection())
                                        {
                                            continue;
                                        }

                                        var tl2 = e.Value.timeline;
                                        if (tl2.getSection() == startln[key].getSection())
                                        {
                                            Note note = startln[key];
                                            ((LongNote)note).setType(lnmode);
                                            LongNote noteend = new LongNote(startln[key].getWav() != wavmap[data] ? wavmap[data] : -2);
                                            tl.setNote(key, noteend);
                                            ((LongNote)note).setPair(noteend);
                                            if (lnlist[key] == null)
                                            {
                                                lnlist[key] = new List<LongNote>();
                                            }
                                            lnlist[key].Add((LongNote)note);

                                            startln[key] = null;
                                            break;
                                        }
                                        else if (tl2.existNote(key))
                                        {
                                            Note note = tl2.getNote(key);
                                            log.Add(new DecodeLog(DecodeLog.State.WARNING, (string)("LN内に通常ノートが存在します。レーン: "
                                                    + (key + 1) + " - Time(ms):" + tl2.getMilliTime())));
                                            tl2.setNote(key, null);
                                            if (note is NormalNote)
                                            {
                                                tl2.addBackGroundNote(note);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (startln[key] == null)
                                {
                                    LongNote ln = new LongNote(wavmap[data]);
                                    ln.setSection(double.MinValue);
                                    startln[key] = ln;
                                    log.Add(new DecodeLog(DecodeLog.State.WARNING, (string)("LN内にLN開始ノートを定義しようとしています : "
                                            + (key + 1) + " - Section : " + tl.getSection() + " - Time(ms):" + tl.getMilliTime())));
                                }
                                else
                                {
                                    if (startln[key].getSection() != double.MinValue)
                                    {
                                        tlcache[(startln[key].getSection())].timeline.setNote(key, null);
                                    }
                                    startln[key] = null;
                                    log.Add(new DecodeLog(DecodeLog.State.WARNING, (string)("LN内にLN終端ノートを定義しようとしています : "
                                            + (key + 1) + " - Section : " + tl.getSection() + " - Time(ms):" + tl.getMilliTime())));
                                }
                            }
                        }), data => IsDefinedSound(wavmap, data), "未定義または空のWAV参照をスキップしました");
                        break;
                    case P1_MINE_KEY_BASE:
                        // mine note
                        this.processData(line, (DataProcessor)((pos, data) =>
                        {
                            var tl = getTimeLine(sectionnum + rate * pos);
                            var insideln = tl.existNote(key);
                            if (!insideln && lnlist[key] != null)
                            {
                                var section = tl.getSection();
                                foreach (var ln in lnlist[key])
                                {
                                    if (ln.getSection() <= section && section <= ln.getPair().getSection())
                                    {
                                        insideln = true;
                                        break;
                                    }
                                }
                            }

                            if (!insideln)
                            {
                                    tl.setNote(key, new MineNote(wavmap[0], data));
                            }
                            else
                            {
                                log.Add(new DecodeLog(DecodeLog.State.WARNING, (string)("地雷ノート追加時に衝突が発生しました : " + (key + 1) + ":"
                                        + tl.getMilliTime())));
                            }
                        }));
                        break;
                    case LANE_AUTOPLAY:
                        // BGレーン
                        this.processData(line, (pos, data) =>
                        {
                            getTimeLine(sectionnum + rate * pos).addBackGroundNote(new NormalNote(wavmap[data]));
                        }, data => IsDefinedSound(wavmap, data), "未定義または空のWAV参照をスキップしました");
                        break;
                    case BGA_PLAY:
                        this.processData(line, (pos, data) =>
                        {
                            getTimeLine(sectionnum + rate * pos).setBGA(bgamap[data]);
                        }, data => IsDefinedBga(bgamap, data), "未定義または空のBMP参照をスキップしました");
                        break;
                    case LAYER_PLAY:
                        this.processData(line, (pos, data) =>
                        {
                            getTimeLine(sectionnum + rate * pos).setLayer(bgamap[data]);
                        }, data => IsDefinedBga(bgamap, data), "未定義または空のBMP参照をスキップしました");
                        break;

                }
            }
        }

        private bool ShouldPrecreateDenseTimelines()
        {
            long objectPairs = 0;
            foreach (string line in channellines)
            {
                objectPairs += GetPairCount(line);
                if (objectPairs >= DenseSectionObjectThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        private void PrecreateDenseTimelines(int[] wavmap, int[] bgamap, int[] cassign, int lnobj)
        {
            var sections = new List<double>();
            foreach (string line in channellines)
            {
                int channel = ResolveTimelineChannel(line, cassign, out int key);
                if (key == -1)
                {
                    continue;
                }

                int dataStart = GetDataStart(line);
                if (dataStart < 0)
                {
                    continue;
                }

                int split = (line.Length - dataStart) / 2;
                for (int i = 0; i < split; i++)
                {
                    int data = ParseBase36Pair(line[dataStart + i * 2], line[dataStart + i * 2 + 1]);
                    if (data > 0 && IsEffectiveTimelineObject(channel, data, wavmap, bgamap, lnobj))
                    {
                        sections.Add(sectionnum + rate * ((double)i / split));
                    }
                }
            }

            if (sections.Count == 0)
            {
                return;
            }

            sections.Sort();
            double previous = double.NaN;
            bool hasPrevious = false;
            foreach (double section in sections)
            {
                if (!hasPrevious || section != previous)
                {
                    getTimeLine(section);
                    previous = section;
                    hasPrevious = true;
                }
            }
        }

        private int ResolveTimelineChannel(string line, int[] cassign, out int key)
        {
            int channel = ChartDecoder.parseInt36(line[(4)], line[(5)]);
            key = 0;
            if (channel >= P1_KEY_BASE && channel < P1_KEY_BASE + 9)
            {
                key = cassign[channel - P1_KEY_BASE];
                return P1_KEY_BASE;
            }
            if (channel >= P2_KEY_BASE && channel < P2_KEY_BASE + 9)
            {
                key = cassign[channel - P2_KEY_BASE + 9];
                return P1_KEY_BASE;
            }
            if (channel >= P1_INVISIBLE_KEY_BASE && channel < P1_INVISIBLE_KEY_BASE + 9)
            {
                key = cassign[channel - P1_INVISIBLE_KEY_BASE];
                return P1_INVISIBLE_KEY_BASE;
            }
            if (channel >= P2_INVISIBLE_KEY_BASE && channel < P2_INVISIBLE_KEY_BASE + 9)
            {
                key = cassign[channel - P2_INVISIBLE_KEY_BASE + 9];
                return P1_INVISIBLE_KEY_BASE;
            }
            if (channel >= P1_LONG_KEY_BASE && channel < P1_LONG_KEY_BASE + 9)
            {
                key = cassign[channel - P1_LONG_KEY_BASE];
                return P1_LONG_KEY_BASE;
            }
            if (channel >= P2_LONG_KEY_BASE && channel < P2_LONG_KEY_BASE + 9)
            {
                key = cassign[channel - P2_LONG_KEY_BASE + 9];
                return P1_LONG_KEY_BASE;
            }
            if (channel >= P1_MINE_KEY_BASE && channel < P1_MINE_KEY_BASE + 9)
            {
                key = cassign[channel - P1_MINE_KEY_BASE];
                return P1_MINE_KEY_BASE;
            }
            if (channel >= P2_MINE_KEY_BASE && channel < P2_MINE_KEY_BASE + 9)
            {
                key = cassign[channel - P2_MINE_KEY_BASE + 9];
                return P1_MINE_KEY_BASE;
            }

            return channel;
        }

        private bool IsEffectiveTimelineObject(int channel, int data, int[] wavmap, int[] bgamap, int lnobj)
        {
            switch (channel)
            {
                case P1_KEY_BASE:
                    return data == lnobj || IsDefinedSound(wavmap, data);
                case P1_INVISIBLE_KEY_BASE:
                case P1_LONG_KEY_BASE:
                case LANE_AUTOPLAY:
                    return IsDefinedSound(wavmap, data);
                case P1_MINE_KEY_BASE:
                    return true;
                case BGA_PLAY:
                case LAYER_PLAY:
                    return IsDefinedBga(bgamap, data);
                default:
                    return false;
            }
        }

        private bool IsDefinedSound(int[] wavmap, int data)
        {
            if ((uint)data >= (uint)wavmap.Length)
            {
                return false;
            }

            int mapped = wavmap[data];
            string[] wavs = model.getWavList();
            return (uint)mapped < (uint)wavs.Length && wavs[mapped].Length > 0;
        }

        private bool IsDefinedBga(int[] bgamap, int data)
        {
            if ((uint)data >= (uint)bgamap.Length)
            {
                return false;
            }

            int mapped = bgamap[data];
            string[] bgas = model.getBgaList();
            return (uint)mapped < (uint)bgas.Length && bgas[mapped].Length > 0;
        }

        private TimeLine getTimeLine(double section)
        {
            var tlc = tlcache.TryGetValue(section, out var t) ? t : null;
            if (tlc != null)
            {
                return tlc.timeline;
            }

            var le = tlcache.lowerEntry(section);
            double scroll = le.Value.timeline.getScroll();
            double bpm = le.Value.timeline.getBPM();
            double time = le.Value.time + le.Value.timeline.getMicroStop() + (240000.0 * 1000 * (section - le.Key)) / bpm;
            TimeLine tl = new TimeLine(section, (long)time, model.getMode().key);
            tl.setBPM(bpm);
            tl.setScroll(scroll);
            Debug.WriteLine(String.Format("le.Key : {0:F6} , section :{1:F6} , bpm : {2:F6} , time : {3}", le.Key, section, bpm, (long)time));
            tlcache.Add(section, new TimeLineCache(time, tl));
            return tl;
        }
    }
}
