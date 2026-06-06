using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CSBMSParser
{
    /**
 * 譜面デコーダー
 * 
 * @author exch
 */
    public abstract class ChartDecoder
    {
        protected int lntype;

        protected List<DecodeLog> log = new List<DecodeLog>();

        /**
		 * パスで指定したファイルをBMSModelに変換する
		 * 
		 * @param path
		 *            譜面ファイルのパス
		 * @return 変換したBMSModel。失敗した場合はnull
		 */
        public virtual BMSModel decode(string path, Encoding encoding)
        {
            return decode(new ChartInformation(path, lntype, null, encoding));
        }

        /**
		 * デコードログを取得する
		 * 
		 * @return デコードログ
		 */
        public virtual List<DecodeLog> getDecodeLog()
        {
            return log;
        }

        public abstract BMSModel decode(ChartInformation info);

        /**
		 * パスで指定したファイルに対応するChartDecoderを取得する
		 * 
		 * @param p
		 *            譜面ファイルのパス
		 * @return 対応するChartDecoder。存在しない場合はnull
		 */
        public static ChartDecoder getDecoder(string p)
        {
            var s = Path.GetFileName(p);
            if (s.EndsWith(".bms", StringComparison.OrdinalIgnoreCase)
                || s.EndsWith(".bme", StringComparison.OrdinalIgnoreCase)
                || s.EndsWith(".bml", StringComparison.OrdinalIgnoreCase)
                || s.EndsWith(".pms", StringComparison.OrdinalIgnoreCase))
            {
                return new BMSDecoder(BMSModel.LNTYPE_LONGNOTE);
            }
            else if (s.EndsWith(".bmson", StringComparison.OrdinalIgnoreCase))
            {
                return new BMSONDecoder(BMSModel.LNTYPE_LONGNOTE);
            }
            return null;
        }

        public static int parseInt36(string s, int index)
        {
            int result = parseInt36(s[(index)], s[(index + 1)]);
            if (result == -1)
            {
                throw new Exception();
            }
            return result;
        }

        public static int parseInt36(char c1, char c2)
        {
            int result = 0;
            if (c1 >= '0' && c1 <= '9')
            {
                result = (c1 - '0') * 36;
            }
            else if (c1 >= 'a' && c1 <= 'z')
            {
                result = ((c1 - 'a') + 10) * 36;
            }
            else if (c1 >= 'A' && c1 <= 'Z')
            {
                result = ((c1 - 'A') + 10) * 36;
            }
            else
            {
                return -1;
            }

            if (c2 >= '0' && c2 <= '9')
            {
                result += (c2 - '0');
            }
            else if (c2 >= 'a' && c2 <= 'z')
            {
                result += (c2 - 'a') + 10;
            }
            else if (c2 >= 'A' && c2 <= 'Z')
            {
                result += (c2 - 'A') + 10;
            }
            else
            {
                return -1;
            }

            return result;
        }

        public class TimeLineCache
        {

            public double time;
            public TimeLine timeline;

            public TimeLineCache(double time, TimeLine timeline)
            {
                this.time = time;
                this.timeline = timeline;
            }
        }
    }
}
