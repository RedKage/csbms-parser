using System;
using System.Collections.Generic;
using System.Text;

namespace CSBMSParser.Bmson
{

    public class BMSInfo
    {
        public string title = ""; // as it is.
        public string subtitle = ""; // self-explanatory
        public string genre = ""; // as it is.
        public string artist = ""; // as it is.
        public string[] subartists = { }; // ["key:value"]
        public string mode_hint = "beat-7k"; // layout hints, e.g. "beat-7k",
                                             // "popn-5k", "generic-nkeys"
        public string chart_name = ""; // e.g. "HYPER", "FOUR DIMENSIONS"
        public double judge_rank = 100; // as defined standard judge width is 100
        public double total = 100; // as it is.
        public double init_bpm; // as it is
        public int level; // as it is?

        public string back_image = ""; // background image filename
        public string eyecatch_image = ""; // eyecatch image filename
        public string banner_image = ""; // banner image filename
        public string preview_music = ""; // preview music filename
        public int resolution = 240; // pulses per quarter note

        public int ln_type;        // LN type

        public double getJudgeRank()
        {
            return judge_rank;
        }

        public void setJudgeRank(int value)
        {
            judge_rank = value;
        }

        public double getInitBPM()
        {
            return init_bpm;
        }

        public void setInitBPM(double bpm)
        {
            init_bpm = bpm;
        }
    }
}
