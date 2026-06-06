using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using CSBMSParser.Bases;

namespace CSBMSParser
{
    public delegate DecodeLog ExecuteCall(BMSModel model, string arg);

    /**
 * BMSファイルをBMSModelにデコードするクラス
 * 
 * @author exch
 */
    public class BMSDecoder : ChartDecoder
    {
        List<string> wavlist = new List<string>(36 * 36);
        private int[] wm = new int[36 * 36];

        List<string> bgalist = new List<string>(36 * 36);
        private int[] bm = new int[36 * 36];

        public BMSDecoder() : this(BMSModel.LNTYPE_LONGNOTE)
        {

        }

        public BMSDecoder(int lntype)
        {
            this.lntype = lntype;
            // 予約語の登録
        }

        public override BMSModel decode(string f, Encoding encoding)
        {
            Logger.getGlobal().fine("BMSファイル解析開始 :" + f);
            try
            {
                BMSModel model = this.decode(f, File.ReadAllBytes(f), f.ToLower().EndsWith(".pms"), null, encoding);
                if (model == null)
                {
                    return null;
                }
                Logger.getGlobal().fine("BMSファイル解析完了 :" + f + " - TimeLine数:" + model.getAllTimes().Length);
                return model;
            }
            catch (IOException e)
            {
                log.Add(new DecodeLog(DecodeLog.State.ERROR, "BMSファイルが見つかりません"));
                Logger.getGlobal().severe("BMSファイル解析中の例外 : " + e.GetType().Name + " - " + e.Message);
            }
            return null;
        }

        public override BMSModel decode(ChartInformation info)
        {
            try
            {
                this.lntype = info.lntype;
                return decode(info.path, File.ReadAllBytes(info.path), info.path.ToLower().EndsWith(".pms"), info.selectedRandoms, info.encoding);
            }
            catch (IOException e)
            {
                log.Add(new DecodeLog(DecodeLog.State.ERROR, "BMSファイルが見つかりません"));
                Logger.getGlobal().severe("BMSファイル解析中の例外 : " + e.GetType().Name + " - " + e.Message);
            }
            return null;
        }


        private List<string>[] lines = new List<string>[1000];

        private Dictionary<int, double> scrolltable = new Dictionary<int, double>();
        private Dictionary<int, double> stoptable = new Dictionary<int, double>();
        private Dictionary<int, double> bpmtable = new Dictionary<int, double>();
        private ArrayDeque<int> randoms = new ArrayDeque<int>();
        private ArrayDeque<int> srandoms = new ArrayDeque<int>();
        private ArrayDeque<int> crandom = new ArrayDeque<int>();
        private ArrayDeque<bool> skip = new ArrayDeque<bool>();

        /**
         * 指定したBMSファイルをモデルにデコードする
         *
         * @param data
         * @return
         */
        public BMSModel decode(byte[] data, bool ispms, int[] random, Encoding encoding)
        {
            return this.decode(null, data, ispms, random, encoding);
        }

        /**
         * 指定したBMSファイルをモデルにデコードする
         *
         * @param data
         * @return
         */
        private BMSModel decode(string path, byte[] data, bool ispms, int[] selectedRandom, Encoding encoding)
        {
            log.Clear();
            var time = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000;
            BMSModel model = new BMSModel();
            scrolltable.Clear();
            stoptable.Clear();
            bpmtable.Clear();

            var memoryStream = new MemoryStream(data);
            using var br = new StreamReader(memoryStream, encoding);

            int maxsec = 0;
            // BMS読み込み、ハッシュ値取得
            try
            {
                model.setMode(ispms ? Mode.POPN_9K : Mode.BEAT_5K);
                // Logger.getGlobal().info(
                // "BMSデータ読み込み時間(ms) :" + (System.currentTimeMillis() - time));

                string line = null;
                wavlist.Clear();
                Array.Fill(wm, -2);
                bgalist.Clear();
                Array.Fill(bm, -2);
                foreach (var e in lines)
                {
                    if (e != null)
                    {
                        e.Clear();
                    }
                }

                randoms.Clear();
                srandoms.Clear();
                crandom.Clear();

                skip.Clear();
                while ((line = br.ReadLine()) != null)
                {
                    if (line.Length < 2)
                    {
                        continue;
                    }

                    if (line[0] == '#')
                    {
                        // line = line.substring(1, line.Length);
                        // RANDOM制御系
                        if (matchesReserveWord(line, "RANDOM"))
                        {
                            try
                            {
                                var r = int.Parse(line.substring(8).Trim());
                                randoms.Add(r);
                                if (selectedRandom != null)
                                {
                                    crandom.Add(selectedRandom[randoms.Count - 1]);
                                }
                                else
                                {
                                    crandom.Add((int)(new Random().NextDouble() * r) + 1);
                                    srandoms.Add(crandom.GetLast());
                                }
                            }
                            catch (Exception e)
                            {
                                log.Add(new DecodeLog(DecodeLog.State.WARNING, "#RANDOMに数字が定義されていません"));
                            }
                        }
                        else if (matchesReserveWord(line, "IF"))
                        {
                            // RANDOM分岐開始
                            if (!crandom.IsEmpty())
                            {
                                try
                                {
                                    skip.Add((crandom.Last() != int.Parse(line.substring(4).Trim())));
                                }
                                catch (Exception e)
                                {
                                    log.Add(new DecodeLog(DecodeLog.State.WARNING, "#IFに数字が定義されていません"));
                                }
                            }
                            else
                            {
                                log.Add(new DecodeLog(DecodeLog.State.WARNING, "#IFに対応する#RANDOMが定義されていません"));
                            }
                        }
                        else if (matchesReserveWord(line, "ENDIF"))
                        {
                            if (!skip.IsEmpty())
                            {
                                skip.RemoveLast();
                            }
                            else
                            {
                                log.Add(new DecodeLog(DecodeLog.State.WARNING, "ENDIFに対応するIFが存在しません: " + line));
                            }
                        }
                        else if (matchesReserveWord(line, "ENDRANDOM"))
                        {
                            if (!crandom.IsEmpty())
                            {
                                crandom.RemoveLast();
                            }
                            else
                            {
                                log.Add(new DecodeLog(DecodeLog.State.WARNING, "ENDRANDOMに対応するRANDOMが存在しません: " + line));
                            }
                        }
                        else if (skip.IsEmpty() || !skip.GetLast())
                        {
                            var c = line[(1)];
                            if ('0' <= c && c <= '9' && line.Length > 6)
                            {
                                // line = line.toUpperCase();
                                // 楽譜
                                var c2 = line[(2)];
                                var c3 = line[(3)];
                                if ('0' <= c2 && c2 <= '9' && '0' <= c3 && c3 <= '9')
                                {
                                    var bar_index = (c - '0') * 100 + (c2 - '0') * 10 + (c3 - '0');
                                    var z = lines[bar_index];
                                    if (z == null)
                                    {
                                        z = lines[bar_index] = new List<string>();
                                    }
                                    z.Add(line);
                                    maxsec = (maxsec > bar_index) ? maxsec : bar_index;
                                }
                                else
                                {
                                    log.Add(new DecodeLog(DecodeLog.State.WARNING, "小節に数字が定義されていません : " + line));
                                }
                            }
                            else if (matchesReserveWord(line, "BPM"))
                            {
                                if (line[(4)] == ' ')
                                {
                                    // BPMは小数点のケースがある(FREEDOM DiVE)
                                    try
                                    {
                                        var arg = line.substring(5).Trim();
                                        double bpm = double.Parse(arg);
                                        if (bpm > 0)
                                        {
                                            model.setBpm(bpm);
                                        }
                                        else
                                        {
                                            log.Add(new DecodeLog(DecodeLog.State.WARNING, "#negative BPMはサポートされていません : " + line));
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        log.Add(new DecodeLog(DecodeLog.State.WARNING, "#BPMに数字が定義されていません : " + line));
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        double bpm = double.Parse(line.substring(7).Trim());
                                        if (bpm > 0)
                                        {
                                            bpmtable.Add(ChartDecoder.parseInt36(line, 4), bpm);
                                        }
                                        else
                                        {
                                            log.Add(new DecodeLog(DecodeLog.State.WARNING, "#negative BPMはサポートされていません : " + line));
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        log.Add(new DecodeLog(DecodeLog.State.WARNING, "#BPMxxに数字が定義されていません : " + line));
                                    }
                                }
                            }
                            else if (matchesReserveWord(line, "WAV"))
                            {
                                // 音源ファイル
                                if (line.Length >= 8)
                                {
                                    try
                                    {
                                        var file_name = line.substring(7).Trim().Replace('\\', '/');
                                        wm[ChartDecoder.parseInt36(line, 4)] = wavlist.Count;
                                        wavlist.Add(file_name);
                                    }
                                    catch (Exception e)
                                    {
                                        log.Add(new DecodeLog(DecodeLog.State.WARNING, "#WAVxxは不十分な定義です : " + line));
                                    }
                                }
                                else
                                {
                                    log.Add(new DecodeLog(DecodeLog.State.WARNING, "#WAVxxは不十分な定義です : " + line));
                                }
                            }
                            else if (matchesReserveWord(line, "BMP"))
                            {
                                // BGAファイル
                                if (line.Length >= 8)
                                {
                                    try
                                    {
                                        var file_name = line.substring(7).Trim().Replace('\\', '/');
                                        bm[ChartDecoder.parseInt36(line, 4)] = bgalist.Count;
                                        bgalist.Add(file_name);
                                    }
                                    catch (Exception e)
                                    {
                                        log.Add(new DecodeLog(DecodeLog.State.WARNING, "#WAVxxは不十分な定義です : " + line));
                                    }
                                }
                                else
                                {
                                    log.Add(new DecodeLog(DecodeLog.State.WARNING, "#BMPxxは不十分な定義です : " + line));
                                }
                            }
                            else if (matchesReserveWord(line, "STOP"))
                            {
                                if (line.Length >= 9)
                                {
                                    try
                                    {
                                        double stop = double.Parse(line.substring(8).Trim()) / 192;
                                        if (stop < 0)
                                        {
                                            stop = Math.Abs(stop);
                                            log.Add(new DecodeLog(DecodeLog.State.WARNING, "#negative STOPはサポートされていません : " + line));
                                        }
                                        stoptable.Add(ChartDecoder.parseInt36(line, 5), stop);
                                    }
                                    catch (Exception e)
                                    {
                                        log.Add(new DecodeLog(DecodeLog.State.WARNING, "#STOPxxに数字が定義されていません : " + line));
                                    }
                                }
                                else
                                {
                                    log.Add(new DecodeLog(DecodeLog.State.WARNING, "#STOPxxは不十分な定義です : " + line));
                                }
                            }
                            else if (matchesReserveWord(line, "SCROLL"))
                            {
                                if (line.Length >= 11)
                                {
                                    try
                                    {
                                        double scroll = double.Parse(line.substring(10).Trim());
                                        scrolltable.Add(ChartDecoder.parseInt36(line, 7), scroll);
                                    }
                                    catch (Exception e)
                                    {
                                        log.Add(new DecodeLog(DecodeLog.State.WARNING, "#SCROLLxxに数字が定義されていません : " + line));
                                    }
                                }
                                else
                                {
                                    log.Add(new DecodeLog(DecodeLog.State.WARNING, "#SCROLLxxは不十分な定義です : " + line));
                                }
                            }
                            else
                            {
                                foreach (var cw in CommandWord.values)
                                {
                                    if (line.Length > cw.Key.Length + 2 && matchesReserveWord(line, cw.Key))
                                    {
                                        DecodeLog log = cw.Value.execute(model, line.substring(cw.Key.Length + 2).Trim());
                                        if (log != null)
                                        {
                                            this.log.Add(log);
                                            Logger.getGlobal()
                                                    .warning(model.getTitle() + " - " + log.getMessage() + " : " + line);
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else if (line[0] == '%')
                    {
                        var index = line.IndexOf(' ');
                        if (index > 0 && line.Length > index + 1)
                        {
                            model.getValues()[line.substring(1, index)] = line.substring(index + 1);
                        }
                    }
                    else if (line[0] == '@')
                    {
                        var index = line.IndexOf(' ');
                        if (index > 0 && line.Length > index + 1)
                        {
                            model.getValues()[line.substring(1, index)] = line.substring(index + 1);
                        }
                    }
                }

                model.setWavList(wavlist.ToArray());
                model.setBgaList(bgalist.ToArray());

                Section prev = null;
                Section[] sections = new Section[maxsec + 1];
                var l = Enumerable.Empty<string>().ToList();
                for (int i = 0; i <= maxsec; i++)
                {
                    sections[i] = new Section(model, prev, lines[i] != null ? lines[i] : l, bpmtable,
                            stoptable, scrolltable, log);
                    prev = sections[i];
                }

                var timelines = new SortedList<double, TimeLineCache>();
                var lnlist = new List<LongNote>[model.getMode().key];
                var lnendstatus = new LongNote[model.getMode().key];
                var basetl = new TimeLine(0, 0, model.getMode().key);
                basetl.setBPM(model.getBpm());
                timelines[0.0] = new TimeLineCache(0.0, basetl);
                foreach (Section section in sections)
                {
                    section.makeTimeLines(wm, bm, timelines, lnlist, lnendstatus);
                }
                // Logger.getGlobal().info(
                // "Section生成時間(ms) :" + (System.currentTimeMillis() - time));
                TimeLine[] tl = new TimeLine[timelines.Count];
                int tlcount = 0;
                foreach (var tlc in timelines.Values)
                {
                    tl[tlcount] = tlc.timeline;
                    tlcount++;
                }
                model.setAllTimeLine(tl);

                if (tl[0].getBPM() == 0)
                {
                    log.Add(new DecodeLog(DecodeLog.State.ERROR, "開始BPMが定義されていないため、BMS解析に失敗しました"));
                    Logger.getGlobal().severe(path + ":BMSファイル解析失敗: 開始BPMが定義されていません");
                    return null;
                }

                for (int i = 0; i < lnendstatus.Length; i++)
                {
                    if (lnendstatus[i] != null)
                    {
                        log.Add(new DecodeLog(DecodeLog.State.WARNING, "曲の終端までにLN終端定義されていないLNがあります。lane:" + (i + 1)));
                        if (lnendstatus[i].getSection() != double.MinValue)
                        {
                            timelines[(lnendstatus[i].getSection())].timeline.setNote(i, null);
                        }
                    }
                }

                if (model.getTotalType() != BMSModel.TotalType.BMS)
                {
                    log.Add(new DecodeLog(DecodeLog.State.WARNING, "TOTALが未定義です"));
                }
                if (model.getTotal() <= 60.0)
                {
                    log.Add(new DecodeLog(DecodeLog.State.WARNING, "TOTAL値が少なすぎます"));
                }
                if (tl.Length > 0)
                {
                    if (tl[tl.Length - 1].getMilliTime() >= model.getLastTime() + 30000)
                    {
                        log.Add(new DecodeLog(DecodeLog.State.WARNING, "最後のノート定義から30秒以上の余白があります"));
                    }
                }
                if (model.getPlayer() > 1 && (model.getMode() == Mode.BEAT_5K || model.getMode() == Mode.BEAT_7K))
                {
                    log.Add(new DecodeLog(DecodeLog.State.WARNING, "#PLAYER定義が2以上にもかかわらず2P側のノーツ定義が一切ありません"));
                }
                if (model.getPlayer() == 1 && (model.getMode() == Mode.BEAT_10K || model.getMode() == Mode.BEAT_14K))
                {
                    log.Add(new DecodeLog(DecodeLog.State.WARNING, "#PLAYER定義が1にもかかわらず2P側のノーツ定義が存在します"));
                }

                var md5 = MD5.Create();
                var sha256 = SHA256.Create();

                model.setMD5(convertHexString(md5.ComputeHash(data)));
                model.setSHA256(convertHexString(sha256.ComputeHash(data)));
                log.Add(new DecodeLog(DecodeLog.State.INFO, "#PLAYER定義が1にもかかわらず2P側のノーツ定義が存在します"));
                Logger.getGlobal().fine("BMSデータ解析時間(ms) :" + ((DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000 - time));

                if (selectedRandom == null)
                {
                    selectedRandom = new int[srandoms.Count];
                    var ri = srandoms.GetEnumerator();
                    for (int i = 0; i < selectedRandom.Length; i++)
                    {
                        ri.MoveNext();
                        selectedRandom[i] = ri.Current;
                    }
                }

                model.setChartInformation(new ChartInformation(path, lntype, selectedRandom, encoding));
                return model;
            }
            catch (IOException e)
            {
                log.Add(new DecodeLog(DecodeLog.State.ERROR, "BMSファイルへのアクセスに失敗しました"));
                Logger.getGlobal()
                        .severe(path + ":BMSファイル解析失敗: " + e.GetType().Name + " - " + e.Message);
            }
            return null;
        }

        private bool matchesReserveWord(string line, string s)
        {
            var len = s.Length;
            if (line.Length <= len)
            {
                return false;
            }
            for (int i = 0; i < len; i++)
            {
                var c = line[(i + 1)];
                var c2 = s[(i)];
                if (c != c2 && c != c2 + 32)
                {
                    return false;
                }
            }
            return true;
        }

        /**
         * バイトデータを16進数文字列表現に変換する
         * 
         * @param data
         *            バイトデータ
         * @returnバイトデータの16進数文字列表現
         */
        public static string convertHexString(byte[] data)
        {
            var sb = new StringBuilder(data.Length * 2);
            foreach (byte b in data)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }
    }

    public class CommandWord
    {
        public static readonly CommandWord PLAYER = new CommandWord()
        {
            execute = (model, arg) =>
            {
                try
                {
                    model.setPlayer(int.Parse(arg));
                }
                catch (FormatException e)
                {
                    return new DecodeLog(DecodeLog.State.WARNING, "#PLAYERに数字が定義されていません");
                }
                return null;
            }
        };
        public static readonly CommandWord GENRE = new CommandWord()
        {
            execute = (model, arg) =>
            {
                model.setGenre(arg);
                return null;
            }
        };
        public static readonly CommandWord TITLE = new CommandWord()
        {
            execute = (model, arg) =>
            {
                model.setTitle(arg);
                return null;
            }
        };
        public static readonly CommandWord SUBTITLE = new CommandWord()
        {
            execute = (model, arg) =>
            {
                model.setSubTitle(arg);
                return null;
            }
        };
        public static readonly CommandWord ARTIST = new CommandWord()
        {
            execute = (model, arg) =>
            {
                model.setArtist(arg);
                return null;
            }
        };
        public static readonly CommandWord SUBARTIST = new CommandWord()
        {
            execute = (model, arg) =>
            {
                model.setSubArtist(arg);
                return null;
            }
        };
        public static readonly CommandWord PLAYLEVEL = new CommandWord()
        {
            execute = (model, arg) =>
            {
                model.setPlaylevel(arg);
                return null;
            }
        };
        public static readonly CommandWord RANK = new CommandWord()
        {
            execute = (model, arg) =>
            {
                try
                {
                    var rank = int.Parse(arg);
                    if (rank >= 0 && rank < 5)
                    {
                        model.setJudgerank(rank);
                        model.setJudgerankType(BMSModel.JudgeRankType.BMS_RANK);
                    }
                    else
                    {
                        return new DecodeLog(DecodeLog.State.WARNING, "#RANKに規定外の数字が定義されています : " + rank);
                    }
                }
                catch (FormatException e)
                {
                    return new DecodeLog(DecodeLog.State.WARNING, "#RANKに数字が定義されていません");
                }
                return null;
            }
        };
        public static readonly CommandWord DEFEXRANK = new CommandWord()
        {
            execute = (model, arg) =>
            {
                try
                {
                    var rank = int.Parse(arg);
                    if (rank >= 1)
                    {
                        model.setJudgerank(rank);
                        model.setJudgerankType(BMSModel.JudgeRankType.BMS_DEFEXRANK);
                    }
                    else
                    {
                        return new DecodeLog(DecodeLog.State.WARNING, "#DEFEXRANK 1以下はサポートしていません" + rank);
                    }
                }
                catch (FormatException e)
                {
                    return new DecodeLog(DecodeLog.State.WARNING, "#DEFEXRANKに数字が定義されていません");
                }
                return null;
            }
        };
        public static readonly CommandWord TOTAL = new CommandWord()
        {
            execute = (model, arg) =>
            {
                try
                {
                    var total = double.Parse(arg);
                    if (total > 0)
                    {
                        model.setTotal(total);
                        model.setTotalType(BMSModel.TotalType.BMS);
                    }
                    else
                    {
                        return new DecodeLog(DecodeLog.State.WARNING, "#TOTALが0以下です");
                    }
                }
                catch (FormatException e)
                {
                    return new DecodeLog(DecodeLog.State.WARNING, "#TOTALに数字が定義されていません");
                }
                return null;
            }
        };
        public static readonly CommandWord VOLWAV = new CommandWord()
        {
            execute = (model, arg) =>
            {
                try
                {
                    model.setVolwav(int.Parse(arg));
                }
                catch (FormatException e)
                {
                    return new DecodeLog(DecodeLog.State.WARNING, "#VOLWAVに数字が定義されていません");
                }
                return null;
            }
        };
        public static readonly CommandWord STAGEFILE = new CommandWord()
        {
            execute = (model, arg) =>
            {
                model.setStagefile(arg.Replace('\\', '/'));
                return null;
            }
        };
        public static readonly CommandWord BACKBMP = new CommandWord()
        {
            execute = (model, arg) =>
            {
                model.setBackbmp(arg.Replace('\\', '/'));
                return null;
            }
        };
        public static readonly CommandWord PREVIEW = new CommandWord()
        {
            execute = (model, arg) =>
            {
                model.setPreview(arg.Replace('\\', '/'));
                return null;
            }
        };
        public static readonly CommandWord LNOBJ = new CommandWord()
        {
            execute = (model, arg) =>
            {
                try
                {
                    var ofs = arg.Select(x => "0123456789abcdefghijklmnopqrstuvwxyz".IndexOf(x)).ToArray();
                    var num = 0;
                    for (int i = 0; i < ofs.Length; i++)
                    {
                        num = ofs[i] * (int)Math.Pow(36, ofs.Length - i - 1) + num;
                    }
                    model.setLnobj(num);
                }
                catch (FormatException e)
                {
                    return new DecodeLog(DecodeLog.State.WARNING, "#PLAYERに数字が定義されていません");
                }
                return null;
            }
        };
        public static readonly CommandWord LNMODE = new CommandWord()
        {
            execute = (model, arg) =>
            {
                try
                {
                    int lnmode = int.Parse(arg);
                    if (lnmode < 0 || lnmode > 3)
                    {
                        return new DecodeLog(DecodeLog.State.WARNING, "#LNMODEに無効な数字が定義されています");
                    }
                    model.setLnmode(lnmode);
                }
                catch (FormatException e)
                {
                    return new DecodeLog(DecodeLog.State.WARNING, "#PLAYERに数字が定義されていません");
                }
                return null;
            }
        };
        public static readonly CommandWord DIFFICULTY = new CommandWord()
        {
            execute = (model, arg) =>
            {
                try
                {
                    model.setDifficulty(int.Parse(arg));
                }
                catch (FormatException e)
                {
                    return new DecodeLog(DecodeLog.State.WARNING, "#DIFFICULTYに数字が定義されていません");
                }
                return null;
            }
        };
        public static readonly CommandWord BANNER = new CommandWord()
        {
            execute = (model, arg) =>
            {
                model.setBanner(arg.Replace('\\', '/'));
                return null;
            }
        };
        public static readonly CommandWord COMMENT = new CommandWord()
        {
            execute = (model, arg) =>
            {
                // TODO 未実装
                return null;
            }
        };

        public static readonly Dictionary<string, CommandWord> Enums = new Dictionary<string, CommandWord>()
        {
            ["PLAYER"] = PLAYER,
            ["GENRE"] = GENRE,
            ["TITLE"] = TITLE,
            ["SUBTITLE"] = SUBTITLE,
            ["ARTIST"] = ARTIST,
            ["SUBARTIST"] = SUBARTIST,
            ["PLAYLEVEL"] = PLAYLEVEL,
            ["RANK"] = RANK,
            ["DEFEXRANK"] = DEFEXRANK,
            ["TOTAL"] = TOTAL,
            ["VOLWAV"] = VOLWAV,
            ["STAGEFILE"] = STAGEFILE,
            ["BACKBMP"] = BACKBMP,
            ["PREVIEW"] = PREVIEW,
            ["LNOBJ"] = LNOBJ,
            ["LNMODE"] = LNMODE,
            ["DIFFICULTY"] = DIFFICULTY,
            ["BANNER"] = BANNER,
            ["COMMENT"] = COMMENT,
        };

        public static Dictionary<string, CommandWord> values => Enums;

        public ExecuteCall execute { get; set; }
    }


    /**
     * 予約語
     *
     * @author exch
     */
    public class OptionWord
    {
        public static readonly OptionWord URL = new OptionWord()
        {
            execute = (_, __) =>
            {
                // TODO 未実装
                return null;
            }
        };

        public ExecuteCall execute { get; set; }
    }
}
