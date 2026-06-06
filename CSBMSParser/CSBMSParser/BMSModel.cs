using System;
using System.Collections.Generic;
using System.Text;

namespace CSBMSParser
{
    public class BMSModel
    {
        /**
		 * プレイヤー数
		 */
        private int player;
        /**
		 * 使用するキー数
		 */
        private Mode mode;
        /**
		 * タイトル名
		 */
        private string title = "";
        /**
		 * サブタイトル名
		 */
        private string subTitle = "";
        /**
		 * ジャンル名
		 */
        private string genre = "";
        /**
		 * アーティスト
		 */
        private string artist = "";
        /**
		 * サブアーティスト
		 */
        private string subartist = "";

        /**
		 * バナー
		 */
        private string banner = "";
        /**
		 * ステージ画像
		 */
        private string stagefile = "";
        private string backbmp = "";
        private string preview = "";
        /**
		 * 標準BPM
		 */
        private double bpm;
        /**
		 * 表記レベル
		 */
        private string playlevel = "";
        /**
		 * 表記ランク(0:beginner, 1:normal, 2:hyper, 3:another, 4:insane)
		 */
        private int difficulty = 0;
        /**
		 * 判定ランク
		 */
        private int judgerank = 2;
        /**
	     * 判定ランクのタイプ
	     */
        private JudgeRankType judgerankType = JudgeRankType.BMS_RANK;
        /**
		 * TOTAL値
		 */
        private double total = 100;
        /**
	     * TOTALのタイプ
	     */
        private TotalType totalType = TotalType.BMSON;
        /**
		 * 標準ボリューム
		 */
        private int volwav;
        /**
		 * MD5値
		 */
        private string md5 = "";
        /**
		 * SHA256値
		 */
        private string sha256 = "";
        /**
		 * WAV定義のIDとファイル名のマップ
		 */
        private string[] wavmap = new string[0];
        /**
		 * BGA定義のIDとファイル名のマップ
		 */
        private string[] bgamap = new string[0];

        private int lnmode = LongNote.TYPE_UNDEFINED;

        private int lnobj = -1;

        public static readonly int LNTYPE_LONGNOTE = 0;
        public static readonly int LNTYPE_CHARGENOTE = 1;
        public static readonly int LNTYPE_HELLCHARGENOTE = 2;

        /**
		 * 時間とTimeLineのマッピング
		 */
        private TimeLine[] timelines = new TimeLine[0];
        private bool timelineStatsValid;
        private double minBpmCache;
        private double maxBpmCache;
        private long lastMilliTimeCache;
        private long lastNoteMilliTimeCache;
        private bool containsUndefinedLongNoteCache;
        private bool containsLongNoteCache;
        private bool containsMineNoteCache;

        private ChartInformation info;

        private Dictionary<string, string> values = new Dictionary<string, string>();

        public BMSModel()
        {
        }

        public int getPlayer()
        {
            return player;
        }

        public void setPlayer(int player)
        {
            this.player = player;
        }

        public string getTitle()
        {
            return title;
        }

        public void setTitle(string title)
        {
            if (title == null)
            {
                this.title = "";
                return;
            }
            this.title = title;
        }

        public string getSubTitle()
        {
            return subTitle;
        }

        public void setSubTitle(string subTitle)
        {
            if (subTitle == null)
            {
                this.subTitle = "";
                return;
            }
            this.subTitle = subTitle;
        }

        public string getGenre()
        {
            return genre;
        }

        public void setGenre(string genre)
        {
            if (genre == null)
            {
                this.genre = "";
                return;
            }
            this.genre = genre;
        }

        public string getArtist()
        {
            return artist;
        }

        public void setArtist(string artist)
        {
            if (artist == null)
            {
                this.artist = "";
                return;
            }
            this.artist = artist;
        }

        public string getSubArtist()
        {
            return subartist;
        }

        public void setSubArtist(string artist)
        {
            if (artist == null)
            {
                this.subartist = "";
                return;
            }
            this.subartist = artist;
        }

        public void setBanner(string banner)
        {
            if (banner == null)
            {
                this.banner = "";
                return;
            }
            this.banner = banner;
        }

        public string getBanner()
        {
            return banner;
        }

        public double getBpm()
        {
            return bpm;
        }

        public void setBpm(double bpm)
        {
            ;
            this.bpm = bpm;
            InvalidateTimelineStats();
        }

        public string getPlaylevel()
        {
            return playlevel;
        }

        public void setPlaylevel(string playlevel)
        {
            this.playlevel = playlevel;
        }

        public int getJudgerank()
        {
            return judgerank;
        }

        public void setJudgerank(int judgerank)
        {
            this.judgerank = judgerank;
        }

        public double getTotal()
        {
            return total;
        }

        public void setTotal(double total)
        {
            this.total = total;
        }

        public int getVolwav()
        {
            return volwav;
        }

        public void setVolwav(int volwav)
        {
            this.volwav = volwav;
        }

        public double getMinBPM()
        {
            EnsureTimelineStats();
            return minBpmCache;
        }

        public double getMaxBPM()
        {
            EnsureTimelineStats();
            return maxBpmCache;
        }

        public void setAllTimeLine(TimeLine[] timelines)
        {
            this.timelines = timelines;
            InvalidateTimelineStats();
        }

        public TimeLine[] getAllTimeLines()
        {
            return timelines;
        }

        public long[] getAllTimes()
        {
            TimeLine[] times = getAllTimeLines();
            long[] result = new long[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                result[i] = times[i].getMilliTime();
            }
            return result;
        }

        public int getLastTime()
        {
            return (int)getLastMilliTime();
        }

        public long getLastMilliTime()
        {
            EnsureTimelineStats();
            return lastMilliTimeCache;
        }

        public int getLastNoteTime()
        {
            return (int)getLastNoteMilliTime();
        }

        public long getLastNoteMilliTime()
        {
            EnsureTimelineStats();
            return lastNoteMilliTimeCache;
        }

        public int getDifficulty()
        {
            return difficulty;
        }

        public void setDifficulty(int difficulty)
        {
            this.difficulty = difficulty;
        }

        public int compareTo(Object arg0)
        {
            return this.title.CompareTo(((BMSModel)arg0).title);
        }

        public string getFullTitle()
        {
            return title + (subTitle != null && subTitle.Length > 0 ? " " + subTitle : "");
        }

        public string getFullArtist()
        {
            return artist + (subartist != null && subartist.Length > 0 ? " " + subartist : "");
        }

        public void setMD5(string hash)
        {
            this.md5 = hash;
        }

        public string getMD5()
        {
            return md5;
        }

        public string getSHA256()
        {
            return sha256;
        }

        public void setSHA256(string sha256)
        {
            this.sha256 = sha256;
        }

        public void setMode(Mode mode)
        {
            this.mode = mode;
            foreach (TimeLine tl in timelines)
            {
                tl.setLaneCount(mode.key);
            }
            InvalidateTimelineStats();
        }

        public Mode getMode()
        {
            return mode;
        }

        public string[] getWavList()
        {
            return wavmap;
        }

        public void setWavList(string[] wavmap)
        {
            this.wavmap = wavmap;
        }

        public string[] getBgaList()
        {
            return bgamap;
        }

        public void setBgaList(string[] bgamap)
        {
            this.bgamap = bgamap;
        }

        public ChartInformation getChartInformation()
        {
            return info;
        }

        public void setChartInformation(ChartInformation info)
        {
            this.info = info;
        }

        public int[] getRandom()
        {
            return info != null ? info.selectedRandoms : null;
        }

        public string getPath()
        {
            return info != null && info.path != null ? info.path : null;
        }

        public int getLntype()
        {
            return info != null ? info.lntype : LNTYPE_LONGNOTE;
        }

        public string getStagefile()
        {
            return stagefile;
        }

        public void setStagefile(string stagefile)
        {
            if (stagefile == null)
            {
                this.stagefile = "";
                return;
            }
            this.stagefile = stagefile;
        }

        public string getBackbmp()
        {
            return backbmp;
        }

        public void setBackbmp(string backbmp)
        {
            if (backbmp == null)
            {
                this.backbmp = "";
                return;
            }
            this.backbmp = backbmp;
        }

        public int getTotalNotes()
        {
            return BMSModelUtils.getTotalNotes(this);
        }

        public bool containsUndefinedLongNote()
        {
            EnsureTimelineStats();
            return containsUndefinedLongNoteCache;
        }

        public bool containsLongNote()
        {
            EnsureTimelineStats();
            return containsLongNoteCache;
        }

        public bool containsMineNote()
        {
            EnsureTimelineStats();
            return containsMineNoteCache;
        }

        public string getPreview()
        {
            return preview;
        }

        public void setPreview(string preview)
        {
            this.preview = preview;
        }

        public EventLane getEventLane()
        {
            return new EventLane(this);
        }

        public Lane[] getLanes()
        {
            int laneCount = mode.key;
            Lane[] lanes = new Lane[laneCount];
            int[] noteCounts = new int[laneCount];
            int[] hiddenCounts = new int[laneCount];

            foreach (TimeLine tl in timelines)
            {
                for (int lane = 0; lane < laneCount; lane++)
                {
                    if (tl.getNote(lane) != null)
                    {
                        noteCounts[lane]++;
                    }
                    if (tl.getHiddenNote(lane) != null)
                    {
                        hiddenCounts[lane]++;
                    }
                }
            }

            Note[][] laneNotes = new Note[laneCount][];
            Note[][] laneHiddens = new Note[laneCount][];
            for (int lane = 0; lane < laneCount; lane++)
            {
                laneNotes[lane] = new Note[noteCounts[lane]];
                laneHiddens[lane] = new Note[hiddenCounts[lane]];
            }

            int[] noteIndexes = new int[laneCount];
            int[] hiddenIndexes = new int[laneCount];
            foreach (TimeLine tl in timelines)
            {
                for (int lane = 0; lane < laneCount; lane++)
                {
                    Note note = tl.getNote(lane);
                    if (note != null)
                    {
                        laneNotes[lane][noteIndexes[lane]] = note;
                        noteIndexes[lane]++;
                    }

                    Note hidden = tl.getHiddenNote(lane);
                    if (hidden != null)
                    {
                        laneHiddens[lane][hiddenIndexes[lane]] = hidden;
                        hiddenIndexes[lane]++;
                    }
                }
            }

            for (int lane = 0; lane < lanes.Length; lane++)
            {
                lanes[lane] = new Lane(laneNotes[lane], laneHiddens[lane]);
            }
            return lanes;
        }

        public int getLnobj()
        {
            return lnobj;
        }

        public void setLnobj(int lnobj)
        {
            this.lnobj = lnobj;
        }

        public int getLnmode()
        {
            return lnmode;
        }

        public void setLnmode(int lnmode)
        {
            this.lnmode = lnmode;
        }

        public Dictionary<string, string> getValues()
        {
            return values;
        }

        public string toChartString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("JUDGERANK:" + judgerank + "\n");
            sb.Append("TOTAL:" + total + "\n");
            if (lnmode != 0)
            {
                sb.Append("LNMODE:" + lnmode + "\n");
            }
            double nowbpm = -double.MinValue;
            StringBuilder tlsb = new StringBuilder();
            foreach (TimeLine tl in timelines)
            {
                tlsb.Clear();
                tlsb.Append(tl.getMilliTime() + ":");
                bool write = false;
                if (nowbpm != tl.getBPM())
                {
                    nowbpm = tl.getBPM();
                    tlsb.Append("B(" + nowbpm + ")");
                    write = true;
                }
                if (tl.getStop() != 0)
                {
                    tlsb.Append("S(" + tl.getStop() + ")");
                    write = true;
                }
                if (tl.getSectionLine())
                {
                    tlsb.Append("L");
                    write = true;
                }

                tlsb.Append("[");
                for (int lane = 0; lane < mode.key; lane++)
                {
                    Note n = tl.getNote(lane);
                    if (n is NormalNote)
                    {
                        tlsb.Append("1");
                        write = true;
                    }
                    else if (n is LongNote ln)
                    {
                        if (!ln.isEnd())
                        {
                            var lnchars = new[] { 'l', 'L', 'C', 'H' };
                            tlsb.Append(lnchars[ln.getType()] + ln.getMilliDuration());
                            write = true;
                        }
                    }
                    else if (n is MineNote)
                    {
                        tlsb.Append("m" + ((MineNote)n).getDamage());
                        write = true;
                    }
                    else
                    {
                        tlsb.Append("0");
                    }
                    if (lane < mode.key - 1)
                    {
                        tlsb.Append(",");
                    }
                }
                tlsb.Append("]\n");

                if (write)
                {
                    sb.Append(tlsb);
                }
            }
            return sb.ToString();
        }

        public JudgeRankType getJudgerankType()
        {
            return judgerankType;
        }

        public void setJudgerankType(JudgeRankType judgerankType)
        {
            this.judgerankType = judgerankType;
        }

        public TotalType getTotalType()
        {
            return totalType;
        }

        public void setTotalType(TotalType totalType)
        {
            this.totalType = totalType;
        }

        internal void InvalidateTimelineStats()
        {
            timelineStatsValid = false;
        }

        private void EnsureTimelineStats()
        {
            if (!timelineStatsValid)
            {
                RebuildTimelineStats();
            }
        }

        private void RebuildTimelineStats()
        {
            double minBpm = bpm;
            double maxBpm = bpm;
            long lastMilliTime = 0;
            long lastNoteMilliTime = 0;
            bool foundLastMilliTime = false;
            bool foundLastNoteMilliTime = false;
            bool containsUndefinedLongNote = false;
            bool containsLongNote = false;
            bool containsMineNote = false;

            for (int i = timelines.Length - 1; i >= 0; i--)
            {
                TimeLine tl = timelines[i];
                double timelineBpm = tl.getBPM();
                minBpm = (minBpm <= timelineBpm) ? minBpm : timelineBpm;
                maxBpm = (maxBpm >= timelineBpm) ? maxBpm : timelineBpm;

                int keys = mode != null ? mode.key : tl.getLaneCount();
                if (keys > tl.getLaneCount())
                {
                    keys = tl.getLaneCount();
                }

                bool hasLaneEvent = false;
                for (int lane = 0; lane < keys; lane++)
                {
                    Note note = tl.getNote(lane);
                    if (note != null)
                    {
                        hasLaneEvent = true;
                        if (!foundLastNoteMilliTime)
                        {
                            lastNoteMilliTime = tl.getMilliTime();
                            foundLastNoteMilliTime = true;
                        }

                        if (note is LongNote ln)
                        {
                            containsLongNote = true;
                            if (ln.getType() == LongNote.TYPE_UNDEFINED)
                            {
                                containsUndefinedLongNote = true;
                            }
                        }
                        else if (note is MineNote)
                        {
                            containsMineNote = true;
                        }
                    }

                    if (tl.getHiddenNote(lane) != null)
                    {
                        hasLaneEvent = true;
                    }
                }

                if (!foundLastMilliTime
                    && (hasLaneEvent || tl.getBackGroundNotes().Count > 0 || tl.getBGA() != -1 || tl.getLayer() != -1))
                {
                    lastMilliTime = tl.getMilliTime();
                    foundLastMilliTime = true;
                }
            }

            minBpmCache = minBpm;
            maxBpmCache = maxBpm;
            lastMilliTimeCache = lastMilliTime;
            lastNoteMilliTimeCache = lastNoteMilliTime;
            containsUndefinedLongNoteCache = containsUndefinedLongNote;
            containsLongNoteCache = containsLongNote;
            containsMineNoteCache = containsMineNote;
            timelineStatsValid = true;
        }

        public enum JudgeRankType
        {
            BMS_RANK, BMS_DEFEXRANK, BMSON_JUDGERANK
        }

        public enum TotalType
        {
            BMS, BMSON
        }
    }
}
