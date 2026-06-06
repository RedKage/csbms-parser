using System;
using System.Collections.Generic;
using System.Text;

namespace CSBMSParser
{
    public static class BMSModelUtils
    {
        public const int TOTALNOTES_ALL = 0;
        public const int TOTALNOTES_KEY = 1;
        public const int TOTALNOTES_LONG_KEY = 2;
        public const int TOTALNOTES_SCRATCH = 3;
        public const int TOTALNOTES_LONG_SCRATCH = 4;
        public const int TOTALNOTES_MINE = 5;

        /**
		 * 総ノート数を返す。
		 * 
		 * @return 総ノート数
		 */
        public static int getTotalNotes(BMSModel model)
        {
            return getTotalNotes(model, 0, int.MaxValue);
        }

        public static int getTotalNotes(BMSModel model, int type)
        {
            return getTotalNotes(model, 0, int.MaxValue, type);
        }

        /**
		 * 指定の時間範囲の総ノート数を返す
		 * 
		 * @param start
		 *            開始時間(ms)
		 * @param end
		 *            終了時間(ms)
		 * @return 指定の時間範囲の総ノート数
		 */
        public static int getTotalNotes(BMSModel model, int start, int end)
        {
            return getTotalNotes(model, start, end, TOTALNOTES_ALL);
        }

        /**
		 * 指定の時間範囲、指定の種類のノートの総数を返す
		 * 
		 * @param start
		 *            開始時間(ms)
		 * @param end
		 *            終了時間(ms)
		 * @param type
		 *            ノートの種類
		 * @return 指定の時間範囲、指定の種類のの総ノート数
		 */
        public static int getTotalNotes(BMSModel model, int start, int end, int type)
        {
            return getTotalNotes(model, start, end, type, 0);
        }

        /**
		 * 指定の時間範囲、指定の種類、指定のプレイサイドのノートの総数を返す
		 * 
		 * @param start
		 *            開始時間(ms)
		 * @param end
		 *            終了時間(ms)
		 * @param type
		 *            ノートの種類
		 * @param side
		 *            プレイサイド(0:両方, 1:1P側, 2:2P側)
		 * @return 指定の時間範囲、指定の種類のの総ノート数
		 */
        public static int getTotalNotes(BMSModel model, int start, int end, int type, int side)
        {
            Mode mode = model.getMode();
            if (mode.player == 1 && side == 2)
            {
                return 0;
            }
            int[] slane = mode.GetScratchKeys(side);
            int[] nlane = mode.GetNonScratchKeys(side);

            int count = 0;
            int lntype = model.getLntype();
            foreach (TimeLine tl in model.getAllTimeLines())
            {
                long time = tl.getMilliTime();
                if (time >= start && time < end)
                {
                    switch (type)
                    {
                        case TOTALNOTES_ALL:
                            count += tl.getTotalNotes(lntype);
                            break;
                        case TOTALNOTES_KEY:
                            foreach (int lane in nlane)
                            {
                                if (tl.getNote(lane) is NormalNote)
                                {
                                    count++;
                                }
                            }
                            break;
                        case TOTALNOTES_LONG_KEY:
                            foreach (int lane in nlane)
                            {
                                if (tl.getNote(lane) is LongNote ln && ShouldCountLongNote(ln, lntype))
                                {
                                    count++;
                                }
                            }
                            break;
                        case TOTALNOTES_SCRATCH:
                            foreach (int lane in slane)
                            {
                                if (tl.getNote(lane) is NormalNote)
                                {
                                    count++;
                                }
                            }
                            break;
                        case TOTALNOTES_LONG_SCRATCH:
                            foreach (int lane in slane)
                            {
                                if (tl.getNote(lane) is LongNote ln && ShouldCountLongNote(ln, lntype))
                                {
                                    count++;
                                }
                            }
                            break;
                        case TOTALNOTES_MINE:
                            foreach (int lane in nlane)
                            {
                                if (tl.getNote(lane) is MineNote)
                                {
                                    count++;
                                }
                            }
                            foreach (int lane in slane)
                            {
                                if (tl.getNote(lane) is MineNote)
                                {
                                    count++;
                                }
                            }
                            break;
                    }
                }
            }
            return count;
        }

        private static bool ShouldCountLongNote(LongNote ln, int lntype)
        {
            return ln.getType() == LongNote.TYPE_CHARGENOTE
                || ln.getType() == LongNote.TYPE_HELLCHARGENOTE
                || (ln.getType() == LongNote.TYPE_UNDEFINED && lntype != BMSModel.LNTYPE_LONGNOTE)
                || !ln.isEnd();
        }

        public static double getAverageNotesPerTime(BMSModel model, int start, int end)
        {
            return (double)BMSModelUtils.getTotalNotes(model, start, end) * 1000 / (end - start);
        }

        public static void changeFrequency(BMSModel model, float freq)
        {
            model.setBpm(model.getBpm() * freq);
            foreach (TimeLine tl in model.getAllTimeLines())
            {
                tl.setBPM(tl.getBPM() * freq);
                tl.setStop((long)(tl.getMicroStop() / freq));
                tl.setMicroTime((long)(tl.getMicroTime() / freq));
            }
            model.InvalidateTimelineStats();
        }

        public static double getMaxNotesPerTime(BMSModel model, int range)
        {
            if (range <= 0)
            {
                return 0;
            }

            int maxnotes = 0;
            TimeLine[] tl = model.getAllTimeLines();
            int lntype = model.getLntype();
            int notes = 0;
            int right = 0;

            for (int left = 0; left < tl.Length; left++)
            {
                long end = tl[left].getMilliTime() + range;
                while (right < tl.Length && tl[right].getMilliTime() < end)
                {
                    notes += tl[right].getTotalNotes(lntype);
                    right++;
                }

                maxnotes = (maxnotes < notes) ? notes : maxnotes;
                notes -= tl[left].getTotalNotes(lntype);
            }

            return maxnotes;
        }

        public static long setStartNoteTime(BMSModel model, long starttime)
        {
            long marginTime = 0;
            foreach (TimeLine tl in model.getAllTimeLines())
            {
                if (tl.getMilliTime() >= starttime)
                {
                    break;
                }
                if (tl.existNote())
                {
                    marginTime = starttime - tl.getMilliTime();
                    break;
                }
            }

            if (marginTime > 0)
            {
                double marginSection = marginTime * model.getAllTimeLines()[0].getBPM() / 240000;
                foreach (TimeLine tl in model.getAllTimeLines())
                {
                    tl.setSection(tl.getSection() + marginSection);
                    tl.setMicroTime(tl.getMicroTime() + marginTime * 1000);
                }

                TimeLine[] tl2 = new TimeLine[model.getAllTimeLines().Length + 1];
                tl2[0] = new TimeLine(0, 0, model.getMode().key);
                tl2[0].setBPM(model.getBpm());
                for (int i = 1; i < tl2.Length; i++)
                {
                    tl2[i] = model.getAllTimeLines()[i - 1];
                }
                model.setAllTimeLine(tl2);
            }

            return marginTime;
        }
    }
}
