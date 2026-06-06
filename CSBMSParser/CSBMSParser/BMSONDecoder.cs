using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CSBMSParser.Bmson;
using Newtonsoft.Json;
using static CSBMSParser.Layer;

namespace CSBMSParser
{
    /**
 * bmsonデコーダー
 * 
 * @author exch
 */
    public class BMSONDecoder : ChartDecoder
    {
        private BMSModel model;

        private SortedList<int, TimeLineCache> tlcache = new SortedList<int, TimeLineCache>();

        public BMSONDecoder() : this(BMSModel.LNTYPE_LONGNOTE)
        {
        }

        public BMSONDecoder(int lntype)
        {
            this.lntype = lntype;
        }

        public override BMSModel decode(ChartInformation info)
        {
            this.lntype = info.lntype;
            return decode(info.path, info.encoding);
        }

        public override BMSModel decode(string f, Encoding encoding)
        {
            Logger.getGlobal().fine("BMSONファイル解析開始 :" + f);
            log.Clear();
            tlcache.Clear();
            var currnttime = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000;
            // BMS読み込み、ハッシュ値取得
            model = new BMSModel();
            Bmson.Bmson bmson = null;
            try
            {
                var digest = SHA256.Create();
                var data = File.ReadAllBytes(f);
                var text = encoding.GetString(data);

                bmson = JsonConvert.DeserializeObject<Bmson.Bmson>(text);
                model.setSHA256(BMSDecoder.convertHexString(digest.ComputeHash(data)));
            }
            catch (Exception e)
            {

                return null;
            }

            model.setTitle(bmson.info.title);
            model.setSubTitle((bmson.info.subtitle != null ? bmson.info.subtitle : "")
                    + (bmson.info.subtitle != null && bmson.info.subtitle.Length > 0 && bmson.info.chart_name != null
                            && bmson.info.chart_name.Length > 0 ? " " : "")
                    + (bmson.info.chart_name != null && bmson.info.chart_name.Length > 0
                            ? "[" + bmson.info.chart_name + "]" : ""));
            model.setArtist(bmson.info.artist);
            StringBuilder subartist = new StringBuilder();
            foreach (string s in bmson.info.subartists)
            {
                subartist.Append((subartist.Length > 0 ? "," : "") + s);
            }
            model.setSubArtist(subartist.ToString());
            model.setGenre(bmson.info.genre);

            if (bmson.info.judge_rank < 0)
            {
                log.Add(new DecodeLog(DecodeLog.State.WARNING, "judge_rankが0以下です。judge_rank = " + bmson.info.judge_rank));
            }
            else if (bmson.info.judge_rank < 5)
            {
                model.setJudgerank(bmson.info.judge_rank);
                log.Add(new DecodeLog(DecodeLog.State.WARNING, "judge_rankの定義が仕様通りでない可能性があります。judge_rank = " + bmson.info.judge_rank));
                model.setJudgerankType(BMSModel.JudgeRankType.BMS_RANK);
            }
            else
            {
                model.setJudgerank(bmson.info.judge_rank);
                model.setJudgerankType(BMSModel.JudgeRankType.BMSON_JUDGERANK);
            }

            if (bmson.info.total > 0)
            {
                model.setTotal(bmson.info.total);
                model.setTotalType(BMSModel.TotalType.BMSON);
            }
            else
            {
                log.Add(new DecodeLog(DecodeLog.State.WARNING, "totalが0以下です。total = " + bmson.info.total));
            }

            model.setBpm(bmson.info.init_bpm);
            model.setPlaylevel(bmson.info.level.ToString());
            model.setMode(Mode.BEAT_7K);
            foreach (var mode in Mode.values.Values)
            {
                if (mode.hint.Equals(bmson.info.mode_hint, StringComparison.InvariantCultureIgnoreCase))
                {
                    model.setMode(mode);
                    break;
                }
            }
            if (bmson.info.ln_type > 0 && bmson.info.ln_type <= 3)
            {
                model.setLnmode(bmson.info.ln_type);
            }
            int[] keyassign;
            if (model.getMode() == Mode.BEAT_5K)
            {
                keyassign = new int[] { 0, 1, 2, 3, 4, -1, -1, 5 };
            }
            else if (model.getMode() == Mode.BEAT_10K)
            {
                keyassign = new int[] { 0, 1, 2, 3, 4, -1, -1, 5, 6, 7, 8, 9, 10, -1, -1, 11 };
            }
            else
            {
                keyassign = new int[model.getMode().key];
                for (int i = 0; i < keyassign.Length; i++)
                {
                    keyassign[i] = i;
                }
            }

            var lnlist = new List<LongNote>[model.getMode().key];
            var lnup = new Dictionary<Bmson.Note, LongNote>();

            model.setBanner(bmson.info.banner_image);
            model.setBackbmp(bmson.info.back_image);
            model.setStagefile(bmson.info.eyecatch_image);
            model.setPreview(bmson.info.preview_music);
            var basetl = new TimeLine(0, 0, model.getMode().key);
            basetl.setBPM(model.getBpm());
            tlcache.Add(0, new TimeLineCache(0.0, basetl));

            if (bmson.bpm_events == null)
            {
                bmson.bpm_events = new BpmEvent[0];
            }
            if (bmson.stop_events == null)
            {
                bmson.stop_events = new StopEvent[0];
            }
            if (bmson.scroll_events == null)
            {
                bmson.scroll_events = new ScrollEvent[0];
            }

            var resolution = bmson.info.resolution > 0 ? bmson.info.resolution * 4 : 960;

            int bpmpos = 0;
            int stoppos = 0;
            int scrollpos = 0;
            // bpmNotes, stopNotes処理
            Array.Sort(bmson.bpm_events);
            Array.Sort(bmson.stop_events);
            Array.Sort(bmson.scroll_events);

            while (bpmpos < bmson.bpm_events.Length || stoppos < bmson.stop_events.Length || scrollpos < bmson.scroll_events.Length)
            {
                var bpmy = bpmpos < bmson.bpm_events.Length ? bmson.bpm_events[bpmpos].y : int.MaxValue;
                var stopy = stoppos < bmson.stop_events.Length ? bmson.stop_events[stoppos].y : int.MaxValue;
                var scrolly = scrollpos < bmson.scroll_events.Length ? bmson.scroll_events[scrollpos].y : int.MaxValue;
                if (scrolly <= stopy && scrolly <= bpmy)
                {
                    getTimeLine(scrolly, resolution).setScroll(bmson.scroll_events[scrollpos].rate);
                    scrollpos++;
                }
                else if (bpmy <= stopy)
                {
                    if (bmson.bpm_events[bpmpos].bpm > 0)
                    {
                        getTimeLine(bpmy, resolution).setBPM(bmson.bpm_events[bpmpos].bpm);
                    }
                    else
                    {
                        log.Add(new DecodeLog(DecodeLog.State.WARNING,
                                "negative BPMはサポートされていません - y : " + bmson.bpm_events[bpmpos].y + " bpm : " + bmson.bpm_events[bpmpos].bpm));
                    }
                    bpmpos++;
                }
                else if (stopy != int.MaxValue)
                {
                    if (bmson.stop_events[stoppos].duration >= 0)
                    {
                        var tl2 = getTimeLine(stopy, resolution);
                        tl2.setStop((long)((1000.0 * 1000 * 60 * 4 * bmson.stop_events[stoppos].duration)
                                / (tl2.getBPM() * resolution)));
                    }
                    else
                    {
                        log.Add(new DecodeLog(DecodeLog.State.WARNING,
                                "negative STOPはサポートされていません - y : " + bmson.stop_events[stoppos].y + " bpm : " + bmson.stop_events[stoppos].duration));
                    }
                    stoppos++;
                }
            }
            // lines処理(小節線)
            if (bmson.lines != null)
            {
                foreach (BarLine bl in bmson.lines)
                {
                    getTimeLine(bl.y, resolution).setSectionLine(true);
                }
            }

            string[] wavmap = new string[bmson.sound_channels.Length + bmson.key_channels.Length + bmson.mine_channels.Length];
            int id = 0;
            long starttime = 0;
            foreach (SoundChannel sc in bmson.sound_channels)
            {
                wavmap[id] = sc.name;
                Array.Sort(sc.notes);
                var Length = sc.notes.Length;
                for (int i = 0; i < Length; i++)
                {
                    var n = sc.notes[i];
                    Bmson.Note next = null;
                    for (int j = i + 1; j < Length; j++)
                    {
                        if (sc.notes[j].y > n.y)
                        {
                            next = sc.notes[j];
                            break;
                        }
                    }
                    long duration = 0;
                    if (!n.c)
                    {
                        starttime = 0;
                    }
                    var tl2 = getTimeLine(n.y, resolution);
                    if (next != null && next.c)
                    {
                        duration = getTimeLine(next.y, resolution).getMicroTime() - tl2.getMicroTime();
                    }

                    var key = n.x > 0 && n.x <= keyassign.Length ? keyassign[n.x - 1] : -1;
                    if (key < 0)
                    {
                        // BGノート
                        tl2.addBackGroundNote(new NormalNote(id, starttime, duration));
                    }
                    else if (n.up)
                    {
                        // LN終端音定義
                        bool assigned = false;
                        if (lnlist[key] != null)
                        {
                            var section = (n.y / resolution);
                            foreach (LongNote ln in lnlist[key])
                            {
                                if (section == ln.getPair().getSection())
                                {
                                    ln.getPair().setWav(id);
                                    ln.getPair().setMicroStarttime(starttime);
                                    ln.getPair().setMicroDuration(duration);
                                    assigned = true;
                                    break;
                                }
                            }
                            if (!assigned)
                            {
                                lnup.Add(n, new LongNote(id, starttime, duration));
                            }
                        }
                    }
                    else
                    {
                        bool insideln = false;
                        if (lnlist[key] != null)
                        {
                            var section = (n.y / resolution);
                            foreach (LongNote ln in lnlist[key])
                            {
                                if (ln.getSection() < section && section <= ln.getPair().getSection())
                                {
                                    insideln = true;
                                    break;
                                }
                            }
                        }

                        if (insideln)
                        {
                            log.Add(new DecodeLog(DecodeLog.State.WARNING,
                                    "LN内にノートを定義しています - x :  " + n.x + " y : " + n.y));
                            tl2.addBackGroundNote(new NormalNote(id, starttime, duration));
                        }
                        else
                        {
                            if (n.l > 0)
                            {
                                // ロングノート
                                TimeLine end = getTimeLine(n.y + n.l, resolution);
                                LongNote ln = new LongNote(id, starttime, duration);
                                if (tl2.getNote(key) != null)
                                {
                                    // レイヤーノート判定
                                    var en = tl2.getNote(key);
                                    if (en is LongNote && end.getNote(key) == ((LongNote)en).getPair())
                                    {
                                        en.addLayeredNote(ln);
                                    }
                                    else
                                    {
                                        log.Add(new DecodeLog(DecodeLog.State.WARNING,
                                                "同一の位置にノートが複数定義されています - x :  " + n.x + " y : " + n.y));
                                    }
                                }
                                else
                                {
                                    bool existNote = false;
                                    foreach (var tl3 in tlcache.subMap(n.y, false, n.y + n.l, true).Values)
                                    {
                                        if (tl3.timeline.existNote(key))
                                        {
                                            existNote = true;
                                            break;
                                        }
                                    }
                                    if (existNote)
                                    {
                                        log.Add(new DecodeLog(DecodeLog.State.WARNING,
                                                "LN内にノートを定義しています - x :  " + n.x + " y : " + n.y));
                                        tl2.addBackGroundNote(new NormalNote(id, starttime, duration));
                                    }
                                    else
                                    {
                                        tl2.setNote(key, ln);
                                        // ln.setDuration(end.getTime() -
                                        // start.getTime());
                                        LongNote lnend = null;
                                        foreach (var up in lnup)
                                        {
                                            if (up.Key.y == n.y + n.l && up.Key.x == n.x)
                                            {
                                                lnend = up.Value;
                                                break;
                                            }
                                        }
                                        if (lnend == null)
                                        {
                                            lnend = new LongNote(-2);
                                        }

                                        end.setNote(key, lnend);
                                        ln.setType(n.t > 0 && n.t <= 3 ? n.t : model.getLnmode());
                                        ln.setPair(lnend);
                                        if (lnlist[key] == null)
                                        {
                                            lnlist[key] = new List<LongNote>();
                                        }
                                        lnlist[key].Add(ln);
                                    }
                                }
                            }
                            else
                            {
                                // 通常ノート
                                if (tl2.existNote(key))
                                {
                                    if (tl2.getNote(key) is NormalNote)
                                    {
                                        tl2.getNote(key).addLayeredNote(new NormalNote(id, starttime, duration));
                                    }
                                    else
                                    {
                                        log.Add(new DecodeLog(DecodeLog.State.WARNING,
                                                "同一の位置にノートが複数定義されています - x :  " + n.x + " y : " + n.y));
                                    }
                                }
                                else
                                {
                                    tl2.setNote(key, new NormalNote(id, starttime, duration));
                                }
                            }
                        }
                    }
                    starttime += duration;
                }
                id++;
            }

            foreach (MineChannel sc in bmson.key_channels)
            {
                wavmap[id] = sc.name;
                Array.Sort(sc.notes);
                var Length = sc.notes.Length;
                for (int i = 0; i < Length; i++)
                {
                    var n = sc.notes[i];
                    var tl3 = getTimeLine(n.y, resolution);

                    var key = n.x > 0 && n.x <= keyassign.Length ? keyassign[n.x - 1] : -1;
                    if (key >= 0)
                    {
                        // BGノート
                        tl3.setHiddenNote(key, new NormalNote(id));
                    }
                }
                id++;
            }
            foreach (MineChannel sc in bmson.mine_channels)
            {
                wavmap[id] = sc.name;
                Array.Sort(sc.notes);
                var Length = sc.notes.Length;
                for (int i = 0; i < Length; i++)
                {
                    var n = sc.notes[i];
                    var tl4 = getTimeLine(n.y, resolution);

                    var key = n.x > 0 && n.x <= keyassign.Length ? keyassign[n.x - 1] : -1;
                    if (key >= 0)
                    {
                        bool insideln = false;
                        if (lnlist[key] != null)
                        {
                            var section = (n.y / resolution);
                            foreach (LongNote ln in lnlist[key])
                            {
                                if (ln.getSection() < section && section <= ln.getPair().getSection())
                                {
                                    insideln = true;
                                    break;
                                }
                            }
                        }

                        if (insideln)
                        {
                            log.Add(new DecodeLog(DecodeLog.State.WARNING,
                                    "LN内に地雷ノートを定義しています - x :  " + n.x + " y : " + n.y));
                        }
                        else if (tl4.existNote(key))
                        {
                            log.Add(new DecodeLog(DecodeLog.State.WARNING,
                                    "地雷ノートを定義している位置に通常ノートが存在します - x :  " + n.x + " y : " + n.y));
                        }
                        else
                        {
                            tl4.setNote(key, new MineNote(id, n.damage));
                        }
                    }
                }
                id++;
            }

            model.setWavList(wavmap);
            // BGA処理
            if (bmson.bga != null && bmson.bga.bga_header != null)
            {
                var bgamap = new string[bmson.bga.bga_header.Length];
                var idmap = new Dictionary<int, int>(bmson.bga.bga_header.Length);
                var seqmap = new Dictionary<int, Layer.Sequence[]>();
                for (int i = 0; i < bmson.bga.bga_header.Length; i++)
                {
                    BGAHeader bh = bmson.bga.bga_header[i];
                    bgamap[i] = bh.name;
                    idmap.Add(bh.id, i);
                }
                if (bmson.bga.bga_sequence != null)
                {
                    foreach (BGASequence n in bmson.bga.bga_sequence)
                    {
                        if (n != null)
                        {
                            Layer.Sequence[] sequence = new Layer.Sequence[n.sequence.Length];
                            for (int i = 0; i < sequence.Length; i++)
                            {
                                var seq = n.sequence[i];
                                if (seq.id != int.MinValue)
                                {
                                    sequence[i] = new Layer.Sequence(seq.time, seq.id);
                                }
                                else
                                {
                                    sequence[i] = new Layer.Sequence(seq.time);
                                }
                            }
                            seqmap.Add(n.id, sequence);
                        }
                    }
                }
                if (bmson.bga.bga_events != null)
                {
                    foreach (BNote n in bmson.bga.bga_events)
                    {
                        getTimeLine(n.y, resolution).setBGA(idmap[(n.id)]);
                    }
                }
                if (bmson.bga.layer_events != null)
                {
                    foreach (BNote n in bmson.bga.layer_events)
                    {
                        int[] idset = n.id_set != null ? n.id_set : new int[] { n.id };
                        var seqs = new Layer.Sequence[idset.Length][];
                        Event @event = default;
                        switch (n.condition != null ? n.condition : "")
                        {
                            case "play":
                                @event = new Event(EventType.PLAY, n.interval);
                                break;
                            case "miss":
                                @event = new Event(EventType.MISS, n.interval);
                                break;
                            default:
                                @event = new Event(EventType.ALWAYS, n.interval);
                                break;
                        }
                        for (int seqindex = 0; seqindex < seqs.Length; seqindex++)
                        {
                            int nid = idset[seqindex];
                            if (seqmap.ContainsKey(nid))
                            {
                                seqs[seqindex] = seqmap[(nid)];
                            }
                            else
                            {
                                seqs[seqindex] = new Layer.Sequence[] { new Layer.Sequence(0, idmap[(n.id)]), new Layer.Sequence(500) };
                            }
                        }
                        getTimeLine(n.y, resolution).setEventlayer(new Layer[] { new Layer(@event, seqs) });
                    }
                }
                if (bmson.bga.poor_events != null)
                {
                    foreach (BNote n in bmson.bga.poor_events)
                    {
                        if (seqmap.ContainsKey(n.id))
                        {
                            getTimeLine(n.y, resolution).setEventlayer(new Layer[] {new Layer(new Layer.Event(EventType.MISS, 1),
                                new Layer.Sequence[][] {seqmap[(n.id)]})});
                        }
                        else
                        {
                            getTimeLine(n.y, resolution).setEventlayer(new[] {
                                new Layer(new Event(EventType.MISS, 1),
                                new [] {
                                    new []{new Layer.Sequence(0, idmap[(n.id)]),new Layer.Sequence(500)}})});
                        }
                    }
                }
                model.setBgaList(bgamap);
            }
            TimeLine[] tl = new TimeLine[tlcache.Count];
            int tlcount = 0;
            foreach (var tlc in tlcache.Values)
            {
                tl[tlcount] = tlc.timeline;
                tlcount++;
            }
            model.setAllTimeLine(tl);

            Logger.getGlobal().fine("BMSONファイル解析完了 :" + f + " - TimeLine数:" + tlcache.Count + " 時間(ms):"
                    + ((DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000 - currnttime));

            model.setChartInformation(new ChartInformation(f, lntype, null, encoding));
            return model;
        }

        public override List<DecodeLog> getDecodeLog()
        {
            return log;
        }

        private TimeLine getTimeLine(int y, double resolution)
        {
            // Timeをus単位にする場合はこのメソッド内部だけ変更すればOK
            var tlc = tlcache.TryGetValue(y, out var t) ? t : default;
            if (tlc != null)
            {
                return tlc.timeline;
            }

            var le = tlcache.lowerEntry(y);
            double bpm = le.Value.timeline.getBPM();
            double time = le.Value.time + le.Value.timeline.getMicroStop()
                    + (240000.0 * 1000 * ((y - le.Key) / resolution)) / bpm;

            TimeLine tl = new TimeLine(y / resolution, (long)time, model.getMode().key);
            tl.setBPM(bpm);
            tlcache.Add(y, new TimeLineCache(time, tl));
            // System.out.println("y = " + y + " , bpm = " + bpm + " , time = " +
            // tl.getTime());
            return tl;
        }
    }

}
