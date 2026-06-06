using System;
using System.Collections.Generic;
using System.Text;

namespace CSBMSParser
{
    public class Mode
    {
        public int id;
        /**
		 * モードの名称。bmsonのmode_hintに対応
		 */
        public string hint;
        /**
         * プレイヤー数
         */
        public int player;
        /**
		 * 使用するキーの数
		 */
        public int key;
        /**
		 * スクラッチキーアサイン
		 */
        public int[] scratchKey;

        private readonly bool[] scratchKeyMap;
        private readonly int[][] scratchKeysBySide;
        private readonly int[][] nonScratchKeysBySide;

        Mode(int id, string hint, int player, int key, int[] scratchKey)
        {
            this.id = id;
            this.hint = hint;
            this.player = player;
            this.key = key;
            this.scratchKey = scratchKey;
            scratchKeyMap = new bool[key];
            foreach (int sc in scratchKey)
            {
                if ((uint)sc < (uint)scratchKeyMap.Length)
                {
                    scratchKeyMap[sc] = true;
                }
            }

            scratchKeysBySide = new int[3][];
            nonScratchKeysBySide = new int[3][];
            for (int side = 0; side < scratchKeysBySide.Length; side++)
            {
                scratchKeysBySide[side] = BuildScratchKeys(side);
                nonScratchKeysBySide[side] = BuildNonScratchKeys(side);
            }
        }


        /**
         * 指定するkeyがスクラッチキーかどうかを返す
         * 
         * @param key キー番号
         * @return スクラッチであればtrue
         */
        public bool isScratchKey(int key)
        {
            return (uint)key < (uint)scratchKeyMap.Length && scratchKeyMap[key];
        }

        internal int[] GetScratchKeys(int side)
        {
            return (uint)side < (uint)scratchKeysBySide.Length ? scratchKeysBySide[side] : scratchKeysBySide[0];
        }

        internal int[] GetNonScratchKeys(int side)
        {
            return (uint)side < (uint)nonScratchKeysBySide.Length ? nonScratchKeysBySide[side] : nonScratchKeysBySide[0];
        }

        private int[] BuildScratchKeys(int side)
        {
            if (side == 2 && player == 1)
            {
                return Array.Empty<int>();
            }

            int count = scratchKey.Length / (side == 0 ? 1 : player);
            int[] result = new int[count];
            int start = side == 2 ? count : 0;
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = scratchKey[start + i];
            }
            return result;
        }

        private int[] BuildNonScratchKeys(int side)
        {
            if (side == 2 && player == 1)
            {
                return Array.Empty<int>();
            }

            int count = (key - scratchKey.Length) / (side == 0 ? 1 : player);
            int[] result = new int[count];
            for (int i = 0, index = 0; index < result.Length; i++)
            {
                if (!isScratchKey(i))
                {
                    result[index] = i;
                    index++;
                }
            }
            return result;
        }


        /**
         * mode_hintに対応するModeを取得する
         * 
         * @param hint
         *            mode_hint
         * @return 対応するMode
         */
        public static Mode getMode(string hint)
        {
            foreach (var pair in values)
            {
                if (pair.Value.hint.Equals(hint))
                {
                    return pair.Value;
                }
            }
            return null;
        }

        public static readonly Dictionary<string, Mode> values = new Dictionary<string, Mode>()
        {
            ["BEAT_5K"] = new Mode(5, "beat-5k", 1, 6, new int[] { 5 }),
            ["BEAT_7K"] = new Mode(7, "beat-7k", 1, 8, new int[] { 7 }),
            ["BEAT_10K"] = new Mode(10, "beat-10k", 2, 12, new int[] { 5, 11 }),
            ["BEAT_14K"] = new Mode(14, "beat-14k", 2, 16, new int[] { 7, 15 }),
            ["POPN_5K"] = new Mode(9, "popn-5k", 1, 5, new int[] { }),
            ["POPN_9K"] = new Mode(9, "popn-9k", 1, 9, new int[] { }),
            ["KEYBOARD_24K"] = new Mode(25, "keyboard-24k", 1, 26, new int[] { 24, 25 }),
            ["KEYBOARD_24K_DOUBLE"] = new Mode(50, "keyboard-24k-double", 2, 52, new int[] { 24, 25, 50, 51 }),
        };

        public static Mode BEAT_5K => values["BEAT_5K"];
        public static Mode BEAT_7K => values["BEAT_7K"];
        public static Mode BEAT_10K => values["BEAT_10K"];
        public static Mode BEAT_14K => values["BEAT_14K"];
        public static Mode POPN_5K => values["POPN_5K"];
        public static Mode POPN_9K => values["POPN_9K"];
        public static Mode KEYBOARD_24K => values["KEYBOARD_24K"];
        public static Mode KEYBOARD_24K_DOUBLE => values["KEYBOARD_24K_DOUBLE"];
    }
}
