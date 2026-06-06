# CSBMSParser 性能优化检查报告

生成日期：2026-06-06

## 范围

本次检查覆盖 `CSBMSParser` 解决方案下的 39 个手写 C# 文件，排除 `bin/` 与 `obj/` 生成文件：

- `CSBMSParser/CSBMSParser/*.cs`
- `CSBMSParser/CSBMSParser/Bases/*.cs`
- `CSBMSParser/CSBMSParser/Bmson/*.cs`
- `CSBMSParser/CSBMSParser.TestConsole/Program.cs`
- `CSBMSParser/CSBMSParser.Benchmark/Program.cs`

重点关注解析 BMS/BMSON、构建 `TimeLine`、查询模型数据、日志输出与临时分配。报告列出的是“可优化点”，不是行为正确性结论；实际修改前建议用现有 benchmark 或新增基准验证收益。

## 优先级总览

| 优先级 | 位置 | 问题 | 建议 |
| --- | --- | --- | --- |
| P0 | `MethodExtensions.lowerEntry`、`descendingMap`、`subMap` | 每次调用都对全部 key 排序或构造新字典，热路径里会退化到 O(n log n) 或 O(n) 额外分配 | 针对 `SortedDictionary` 写专用遍历/缓存，或替换为支持 predecessor/range view 的结构 |
| P0 | `Section.getTimeLine` / `BMSONDecoder.getTimeLine` | 每次创建 timeline 都调用 `lowerEntry`，间接排序全部 key | 利用 `SortedDictionary` 已排序特性维护上一个 timeline，或改用二分数组/有序列表 |
| P0 | `Section.makeTimeLines` LNOBJ 与 LN 处理 | 多处反向扫描全部 timeline、线性扫描每 lane 的 LN 列表 | 按 lane 保存最近 note/LN 起点和活动 LN 区间，避免重复全表扫描 |
| P0 | `BMSDecoder.decode` 解析头部 | 大量 `substring().Trim()`、`double.Parse/int.Parse` 异常驱动解析、逐命令遍历 `CommandWord.values` | 使用 `ReadOnlySpan<char>`、`TryParse`、命令前缀直接分派，减少分配与异常 |
| P1 | `BMSONDecoder.decode` | 整文件 `GetString` 后反序列化、notes 查找 next 使用内层线性扫描、LN 区间多次线性判断 | 用 stream reader 反序列化；预处理 next continuation；按 lane 维护活动 LN |
| P1 | `BMSModelUtils.getMaxNotesPerTime` | 滑动窗口用双重循环，最坏 O(n^2) | 改为双指针窗口并维护滚动计数 |
| P1 | `Logger` / `Debug.WriteLine` | 默认 logger 写控制台，解析热路径和 benchmark 容易被 I/O 污染 | 默认静默或可配置日志级别；Debug 输出改成条件编译或删除 |
| P1 | `Lane` / `EventLane` 构造 | 每次调用都扫描全部 timeline、用 Queue 后 ToArray 二次分配 | 缓存在 `BMSModel` 内，或先计数再一次性填数组 |
| P2 | `BMSModel` 查询 API | `getAllTimes` 每次分配数组，`contains*`、`getLast*` 多次全表扫描 | 缓存常用统计，或提供无分配枚举/计数 API |
| P2 | `TimeLine` / `Note` 背景与 layered note | 共享空 `List` 作哨兵，添加时新建 List；`removeBackGroundNote` 当前实现低效且有索引风险 | 使用 `Array.Empty<T>`/只读空集合；移除直接 `RemoveAt` |

## 逐文件检查

### `CSBMSParser/CSBMSParser/BMSDecoder.cs`

- `decode(string, Encoding)` 与 `decode(ChartInformation)` 使用 `File.ReadAllBytes`，文件 I/O 场景会一次性占用整文件内存。已有 byte-array overload 适合纯解析 benchmark；若要解析超大谱面，可增加 stream/span 解析入口。
- `f.ToLower().EndsWith(".pms")` 会分配小写字符串，建议使用 `EndsWith(".pms", StringComparison.OrdinalIgnoreCase)`。
- `Logger.getGlobal().fine(... model.getAllTimes().Length)` 会调用 `getAllTimes()` 分配 `long[]`，只需要数量时应使用 `getAllTimeLines().Length`。
- `DateTime.Now.ToUniversalTime().Ticks` 仅用于日志耗时，热路径可用 `Stopwatch.GetTimestamp()` 或在日志关闭时完全跳过。
- `new MemoryStream(data)` + `StreamReader.ReadLine()` 会逐行创建字符串。继续保持兼容编码时可接受；若追求解析极限，应考虑 span line scanner 或复用缓冲。
- `lines` 固定 1000 个小节，重用时遍历全部清空。可只记录本次用到的小节索引并清理这些列表；也要考虑超过 999 小节时的扩容策略。
- `#RANDOM` 分支里每次 `new Random()`，短时间多次创建会慢且随机质量差。应把 `Random` 作为 decoder 字段或使用 `Random.Shared`。
- `matchesReserveWord` 被大量重复调用，普通 header 命令还要遍历 `CommandWord.values`。可先读取 `#` 后的命令 token，再 switch/字典查找。
- 解析数值大量使用 `line.substring(...).Trim()` + `int.Parse/double.Parse` + catch。建议改为 `ReadOnlySpan<char>` 与 `TryParse`，指定 `CultureInfo.InvariantCulture`。
- `#WAV/#BMP/#STAGEFILE/#BACKBMP/#PREVIEW/#BANNER` 多次 `Replace('\\','/')` 会在没有反斜杠时仍扫描字符串；可先 `IndexOf('\\')`，必要时替换。
- `model.setWavList(wavlist.ToArray())` 与 `setBgaList` 必须把 List 转数组；如果解析期能直接填定长表或记录最大 ID，可减少拷贝。
- `var l = Enumerable.Empty<string>().ToList()` 为所有空小节创建一个可变空 List。建议使用静态 `Array.Empty<string>()` 或只读空集合，并调整 `Section` 构造参数类型。
- 为 `0..maxsec` 每个小节都创建 `Section`，空小节很多时仍产生对象。可只为有数据的小节创建 section，同时保留小节长度推进状态。
- 时间线使用 `SortedDictionary<double, TimeLineCache>`，后续 `lowerEntry` 每次排序全部 key，是当前最明显热点之一。
- MD5 与 SHA256 在解析完成后对同一 `byte[]` 各扫描一次。如果调用方不总需要 hash，可延迟计算；如果需要，可用 `HashAlgorithm.ComputeHash` 的流式管线或只保留 SHA256。
- `convertHexString` 用 `b.ToString("X2")` 每字节分配小字符串。可改为 `Convert.ToHexString(data)`。
- `LNOBJ` 解析使用 LINQ `Select(...).ToArray()` 和 `Math.Pow`，应改为手写 base36 累乘。
- `CommandWord` 中每个命令都是 delegate 调用；解析密集时可用 switch 静态方法减少 delegate 间接调用。

### `CSBMSParser/CSBMSParser/Section.cs`

- 构造函数对每条 channel line 都遍历 `NOTE_CHANNELS` 数组判断类型。可用范围判断或预计算 channel 分类表。
- `processData` 使用 delegate/lambda，`makeTimeLines` 对每种 channel 都创建闭包并在每个数据点回调。可把 channel 分支写成直接循环，减少委托和闭包开销。
- `splitData` 与 `processData` 每次 `line.IndexOf(":")`；同一 line 后续还会再次解析 channel 和数据。可把 line 预解析为 channel + data span。
- `bpmchange/stop/scroll` 用 `SortedDictionary<double,double>`，但每个 section 内数据本来按 channel 顺序扫描，可考虑保留排序数组或普通 List 后排序。
- `makeTimeLines` 中 `tlcache.descendingMap()` 在 LNOBJ 和 LN 终端处理中多次调用；当前实现每次都排序全部 keys。应按 lane 维护“最近可匹配 note/LN start”，或在 `SortedDictionary` 上反向遍历。
- `lnlist[key]` 用 List 保存所有已完成 LN，之后每次判断 `insideln` 都线性扫描。可维护当前活动 LN，或按 lane 用区间索引。
- `getTimeLine` 每次 miss 都调用 `tlcache.lowerEntry(section)`，当前是全量排序。应利用时间线创建大多单调前进的事实缓存 last entry，或换成可二分的有序数组。
- `Debug.WriteLine(String.Format(...))` 即使 Debug 输出不显示，也会构造格式化字符串。建议删除或包在 `#if DEBUG` 且避免热路径格式化。
- `getTimeLine(sectionnum + rate * pos)` 使用 `double` 作字典 key，有精度和比较成本；可考虑把小节位置转为整数 tick/分母 pair。
- BGA/Layer/BG note channel 使用相同 `processData` 模式，可合并直接解析逻辑。

### `CSBMSParser/CSBMSParser/BMSONDecoder.cs`

- `File.ReadAllBytes` + `encoding.GetString` + `JsonConvert.DeserializeObject` 会同时持有 byte[] 与 string。可用 `StreamReader` + `JsonTextReader` 流式反序列化，尤其适合大 bmson。
- 创建 SHA256 后未 `Dispose`。性能影响小，但建议 `using`；也可以把 hash 计算变为可选或延迟。
- `StringBuilder subartist` 每次 append 时构造 `"," + s`，可直接 `Append(',').Append(s)` 或 `string.Join(",", bmson.info.subartists)`。
- `foreach (var mode in Mode.values.Values)` 匹配 `mode_hint` 可改为 `Mode.getMode` 或预构建 hint 字典，避免每次线性扫描。
- `Array.Sort` 会原地改变 DTO 数组。性能可接受，但若调用方依赖原顺序需注意；可在反序列化后统一排序。
- sound channel 中查找 next continuation 采用内层 `for (j=i+1...)`，在长 notes 数组下最坏 O(n^2)。排序后可直接检查下一个 `notes[i+1]` 或预处理每个 note 的 next continuation。
- `lnlist[key]` 和 `lnup` 在多个路径中线性查找。`lnup` 可用 `(x,y)` 或 `(x, y+l)` 作为字典 key；已完成 LN 区间可按 lane 建索引。
- `tlcache.subMap(...).Values` 当前会构造新 `Dictionary`，并且 `subMap` 条件疑似与命名相反。应提供无分配 range iterator。
- BGA layer fallback 多处 `new Layer.Sequence[] { ... }`、`new Layer[] { ... }`；可在事件多时通过 helper 减少重复分配，但收益低于 timeline/LN 热点。
- `seqmap.ContainsKey(nid)` 后再 `seqmap[nid]` 是双查找，改为 `TryGetValue`。
- `getTimeLine(int y, double resolution)` 同样依赖 `lowerEntry` 全量排序，是 BMSON 解析的主要热点之一。
- 日志耗时同样建议用 `Stopwatch` 或在 logger 关闭时跳过。

### `CSBMSParser/CSBMSParser/MethodExtensions.cs`

- `descendingMap` 对任意 `IDictionary` 执行 `Keys.OrderByDescending(...).Select(...)`，每次调用都排序并产生 LINQ 迭代器。热路径中应避免。
- `lowerEntry` 每次 `OrderByDescending(...).FirstOrDefault(...)`，对 timeline 查找非常昂贵。对 `SortedDictionary` 应实现专用 predecessor 查找，或用 `SortedList` + 二分。
- `subMap` 每次创建新 `Dictionary`，并使用 LINQ `Where`。BMSON LN 区间检查应改成 yield iterator 或显式 range scan。
- `substring` 扩展只是转发到 `String.Substring`，会鼓励大量分配。热路径应替换成 span 解析。

### `CSBMSParser/CSBMSParser/BMSModel.cs`

- `getAllTimes()` 每次分配新 `long[]`。需要数量时用 `getAllTimeLines().Length`；需要遍历时可增加无分配枚举。
- `getMinBPM/getMaxBPM/getLastMilliTime/getLastNoteMilliTime/contains*` 都扫描全部 timeline。若调用频繁，可在 `setAllTimeLine` 后缓存统计。
- `getLastMilliTime` 内部对每个 lane 调 `tl.getBackGroundNotes().Count`，背景 note 与 BGA/layer 的判断放在 lane 循环内，会重复检查同一个 timeline 的非 lane 状态。可先检查 BG/BGA/layer，再扫 lane。
- `setMode` 会遍历所有 timeline 并调用 `setLaneCount`，解析过程中多次 mode 升级会反复复制 note 数组。可在确定最终 mode 后统一设置，或让 section 预扫描只确定 mode。
- `getEventLane()`、`getLanes()` 每次调用都重新扫描 timeline 构造对象。可缓存并在 timeline 变更时失效。
- `toChartString()` 多处字符串拼接传给 `StringBuilder.Append`，如 `"JUDGERANK:" + judgerank`。可链式 `Append` 减少临时字符串；`lnchars` 数组也可静态化。

### `CSBMSParser/CSBMSParser/BMSModelUtils.cs`

- `getTotalNotes` 每次调用都会构造 scratch lane 和 normal lane 数组。可把 lane 分组缓存到 `Mode` 或静态表。
- `getTotalNotes` 对每个 timeline 多次调用 `getMilliTime()`、`getLntype()`、`getNote()`。可局部缓存值。
- `getMaxNotesPerTime` 是双重循环。应改为双指针滑动窗口：右指针推进到范围外，左指针前进时扣减计数。
- `setStartNoteTime` 多次调用 `model.getAllTimeLines()`，并整体平移每个 timeline。可先缓存数组引用；大模型下可考虑记录全局 offset。

### `CSBMSParser/CSBMSParser/TimeLine.cs`

- `setMicroTime` 与 `setSection` 会遍历 notes、hiddennotes、bgnotes 同步子 note。若批量调整时间，可考虑延迟 offset 或批量 API。
- `setLaneCount` 手写复制数组，可用 `Array.Copy`，并避免 mode 多次变化触发多轮复制。
- `addBackGroundNote` 使用共享可变 `Note.EMPTYARRAY` 作为哨兵。建议使用 `null` 或 `Array.Empty<Note>`/只读空集合，减少误改风险。
- `removeBackGroundNote` 创建 `new List<Note>(count - 1)` 后按索引赋值，当前容量不等于 Count，存在异常风险；性能上应直接 `RemoveAt(i)`。
- `setBPM` 内 `Debug.WriteLine(String.Format(...))` 在热路径里会格式化字符串，建议条件编译或删除。

### `CSBMSParser/CSBMSParser/ChartDecoder.cs`

- `getDecoder` 使用 `Path.GetFileName(p).ToLower()` 分配小写字符串。建议 `Path.GetExtension(p)` + `StringComparer.OrdinalIgnoreCase` 或 `EndsWith(..., OrdinalIgnoreCase)`。
- `parseInt36(string, int)` 对非法值直接 `throw new Exception()`，解析热路径中异常成本高。可提供 `TryParseInt36`，由调用方决定日志。
- `parseInt36(char,char)` 可用静态 lookup table 把 char 映射到 0..35，减少分支。

### `CSBMSParser/CSBMSParser/Lane.cs`

- 构造时用两个 `Queue<Note>` 收集再 `ToArray()`，会有额外增长与拷贝。可先统计数量再填数组，或用 `List<Note>` 并预估容量。
- 每次 `new Lane(model, lane)` 都扫描全部 timeline。若调用频繁，应由 `BMSModel` 缓存 lane 数组。
- `mark(int time)` 线性前后移动，适合时间单调访问；随机 seek 多时可改为二分查找。

### `CSBMSParser/CSBMSParser/EventLane.cs`

- 构造时使用 `Queue<TimeLine>` 后 `ToArray()`，可改成预统计或 `List<TimeLine>`。
- 每次 `new EventLane(model)` 都扫描全部 timeline，应考虑缓存。
- `mark(int time)` 的比较方向与 `Lane.mark` 相反，若频繁 seek 需要确认行为并可改二分。

### `CSBMSParser/CSBMSParser/Note.cs`

- `EMPTYARRAY` 是可变 `List<Note>`，作为共享空列表不够安全。建议替换为 `Array.Empty<Note>()` 或 `IReadOnlyList<Note>`。
- `addLayeredNote` 第一次添加时分配 `List<Note>`，这是合理的懒分配；大量 layered notes 时可传入预估容量。
- `Clone` 使用 `MemberwiseClone`，会浅拷贝 `layerednotes`；若 clone 用于高频路径，既有性能也有共享状态风险。

### `CSBMSParser/CSBMSParser/LongNote.cs`

- `Clone()` 递归 clone pair，若在热路径使用会产生额外对象；目前解析主路径未见频繁 clone。
- `setPair` 会读取双方 section 并设置 type，性能问题不大。

### `CSBMSParser/CSBMSParser/NormalNote.cs`

- 仅构造函数设置字段，无明显独立性能热点。

### `CSBMSParser/CSBMSParser/MineNote.cs`

- 仅保存 damage，无明显独立性能热点。

### `CSBMSParser/CSBMSParser/Mode.cs`

- `isScratchKey` 对 scratchKey 线性扫描。当前 scratch 数量很小，影响低；如果被高频调用，可预计算 bool[]。
- `getMode` 遍历 `values`，可用按 `hint` 建的静态字典。
- 静态属性如 `BEAT_5K => values["BEAT_5K"]` 每次字典查找，可改为静态 readonly 字段。

### `CSBMSParser/CSBMSParser/Layer.cs`

- BGA event/layer 数据容器本身无明显热点。若 BMSON BGA 事件很多，可把常见 `Event` / end `Sequence` 复用，但优先级低。

### `CSBMSParser/CSBMSParser/Logger.cs`

- 默认 `fine/warning/severe` 直接 `Console.WriteLine`。解析大量 warning 或 benchmark 时 I/O 会严重污染性能。建议默认 no-op 或增加日志级别/handler。

### `CSBMSParser/CSBMSParser/BMSGenerator.cs`

- `generate(int[] random, Encoding encoding)` 忽略字段 `this.random`，每次创建新 `BMSDecoder`。如果用于同一数据多次随机生成，可复用 decoder 需要先确认 decoder 状态完全清理。
- 性能热点主要仍在 `BMSDecoder.decode`。

### `CSBMSParser/CSBMSParser/Bases/ArrayDeque.cs`

- 继承 `List<T>` 暴露过多 API；`GetLast()` 调用 LINQ `LastOrDefault()`，可直接 `this[Count - 1]`。
- `RemoveLast()` 对 List 尾部删除是 O(1)，可保留。

### `CSBMSParser/CSBMSParser/DecodeLog.cs`

- 简单日志对象。大量 warning 时会产生对象分配；可考虑日志采样、对象池或按需关闭 warning，但常规解析下不是首要热点。

### `CSBMSParser/CSBMSParser/ChartInformation.cs`

- 简单数据对象，无明显性能热点。可改 record/readonly 主要是可维护性，不是性能优先项。

## Bmson DTO 文件

以下文件主要是 JSON 反序列化数据容器，本身没有复杂逻辑，独立性能优化空间较小：

- `Bmson/BarLine.cs`
- `Bmson/BGAHeader.cs`
- `Bmson/BGASequence.cs`
- `Bmson/BMSInfo.cs`
- `Bmson/Bmson.cs`
- `Bmson/BNote.cs`
- `Bmson/BpmEvent.cs`
- `Bmson/MineChannel.cs`
- `Bmson/MineNote.cs`
- `Bmson/Note.cs`
- `Bmson/ScrollEvent.cs`
- `Bmson/Sequence.cs`
- `Bmson/SoundChannel.cs`
- `Bmson/StopEvent.cs`

可考虑的低优先级优化：

- DTO 数组字段默认值可统一为 `Array.Empty<T>()`，减少每个 `Bmson` 对象的空数组分配。
- `BMSONObject.CompareTo` 使用 `return y - other.y`，存在整数溢出理论风险；可用 `y.CompareTo(other.y)`。性能差异可忽略。
- `BGA.cs` 中 getter/setter 与 public field 重复，不影响热路径；可精简 API 降低维护成本。

### `CSBMSParser/CSBMSParser/Bmson/BMSONObject.cs`

- `CompareTo` 可改为 `y.CompareTo(other.y)`。这主要是正确性和健壮性，性能收益不明显。

### `CSBMSParser/CSBMSParser/Bmson/BGA.cs`

- 数据容器，无明显独立性能热点。若 BGA 数据量很大，优化应在 `BMSONDecoder` 里减少事件数组分配和字典双查找。

## 非核心项目

### `CSBMSParser/CSBMSParser.TestConsole/Program.cs`

- 第 19 行用 LINQ `Select/Concat/SelectMany/OfType/OrderBy/ToArray` 聚合所有 note，会产生多层迭代器和排序分配。作为测试控制台可接受；若用于性能分析，应改为手写循环并避免排序。
- 大量 `Console.WriteLine` 是 I/O 主导，不应用于解析性能测量。
- 路径硬编码且 `Console.ReadLine()` 阻塞，不影响类库性能。

### `CSBMSParser/CSBMSParser.Benchmark/Program.cs`

- `GlobalSetup` 读取 fixture bytes，benchmark 方法不包含文件 I/O，设计合理。
- `GlobalSetup` 预解码一次会计入 setup，不计入 benchmark，合理。
- 每次 `Decode()` 新建 `BMSDecoder`，能隔离 decoder 状态；如果要测“复用 decoder”场景，可另加 benchmark。
- 当前返回 `getAllTimeLines().Length`，避免 `getAllTimes()` 分配，合理。
- 已设置静默 logger，避免控制台 I/O 污染 benchmark。

## 建议实施顺序

1. 先优化 `MethodExtensions.lowerEntry/descendingMap/subMap` 与 timeline 查找，这是 BMS/BMSON 共有热点。
2. 改写 `Section.makeTimeLines` 的 LNOBJ/LN 匹配：按 lane 保存最近 note、活动 LN、已完成 LN 区间。
3. 把 `BMSDecoder` header 解析从 `substring/Trim/Parse/catch` 改成 span + `TryParse` + 直接命令分派。
4. 优化 `BMSONDecoder`：stream JSON、next continuation 预处理、`TryGetValue`、无分配 range scan。
5. 优化查询 API：`BMSModelUtils.getMaxNotesPerTime` 双指针、`BMSModel` 缓存 lanes/event lane/统计。
6. 清理日志与 Debug 输出，让 benchmark 和生产解析默认不做控制台 I/O。

## 验证建议

- 使用现有 benchmark 项目固定四个 fixture 对比：

```powershell
dotnet run -c Release --project .\CSBMSParser\CSBMSParser.Benchmark\CSBMSParser.Benchmark.csproj -- --filter *BmsDecodeBenchmarks*
```

- 每完成一个优化阶段，至少比较：
  - `Mean`
  - `Allocated`
  - Gen0/Gen1 次数
  - 四个 fixture 的 timeline 数量、总 note 数、hash 字段是否保持一致

- 对 `MethodExtensions` 和 `Section` 的改动建议补充单元测试，覆盖：
  - BPM/STOP/SCROLL 时间线顺序
  - LNOBJ
  - LN channel
  - mine note 与 LN 冲突
  - BGA/layer/poor event

## P0 实施记录与基准对比

本次先实现并验证 P0 中影响最大的公共热点：timeline cache 的 predecessor 查找与反向遍历。未改 `CSBMSParser` 类库 public API，未在本阶段改写 header span 解析或 LN 状态机。

### 代码改动

| 文件 | 改动 | 直接影响 |
| --- | --- | --- |
| `CSBMSParser/CSBMSParser/BMSDecoder.cs` | `timelines` 从 `SortedDictionary<double, TimeLineCache>` 改为 `SortedList<double, TimeLineCache>` | BMS/BML 解析期 timeline cache 使用可索引有序数组 |
| `CSBMSParser/CSBMSParser/Section.cs` | `tlcache` 字段与 `makeTimeLines` 参数同步改为 `SortedList<double, TimeLineCache>` | `Section.getTimeLine` 的最近前序 timeline 查询进入二分快路径 |
| `CSBMSParser/CSBMSParser/BMSONDecoder.cs` | `tlcache` 从 `Dictionary<int, TimeLineCache>` 改为 `SortedList<int, TimeLineCache>` | BMSON timeline cache 也能使用同一 `lowerEntry` 快路径 |
| `CSBMSParser/CSBMSParser/MethodExtensions.cs` | 移除 `System.Linq` 依赖；`lowerEntry` 对 `SortedList` 使用二分 predecessor；`descendingMap` 对 `SortedList` 使用倒序索引；`subMap` 改为显式循环 | 去掉热路径中的 `OrderByDescending`、LINQ 迭代器和排序临时分配 |

原始热点来自 `MethodExtensions.lowerEntry`：

```csharp
self.Keys.OrderByDescending(x => x).FirstOrDefault(...)
```

`Section.getTimeLine` 和 `BMSONDecoder.getTimeLine` 每次创建新 timeline 时都会调用 `lowerEntry` 找前一个 timeline。旧实现会在每次查询时重新排序全部 key；改用 `SortedList` 后，`lowerEntry` 通过二分查找最大 `< key` 的索引，查询成本从重复全量排序变成对有序数组的二分。

`subMap` 特别说明：本次保持既有范围判断语义，只去掉 LINQ `Where` 并为 `SortedList` 增加直接索引遍历路径。没有借优化修正疑似范围语义问题，避免在缺少 BMSON fixture 的情况下改变行为。

### Benchmark 对比

Benchmark 命令：

```powershell
dotnet .\CSBMSParser\CSBMSParser.Benchmark\bin\Release\net10.0\CSBMSParser.Benchmark.dll --job Short --filter *BmsDecodeBenchmarks*
dotnet .\.verify-p0-final3\bin\CSBMSParser.Benchmark.dll --job Short --filter *BmsDecodeBenchmarks*
```

结果来自 BenchmarkDotNet `ShortRun`，每个 case `IterationCount=3`，数值适合快速对比，不等价于最终发布基准。运行环境摘要：BenchmarkDotNet 0.15.8、.NET 10.0.8、Windows 11、AMD Ryzen 7 5800X。

| Fixture | P0 前 Mean | P0 后 Mean | 加速比 | P0 前 Allocated | P0 后 Allocated | 分配下降 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `_ANOTHER.bms` | 16.21 ms | 2.575 ms | 6.30x | 7.33 MB | 771.04 KB | 约 89.7% |
| `AF_7KSHD.bms` | 60.72 ms | 8.066 ms | 7.53x | 22.17 MB | 1591.27 KB | 约 93.0% |
| `Lapras_A.bml` | 60.94 ms | 4.969 ms | 12.26x | 17.34 MB | 1398.70 KB | 约 92.1% |
| `tetrinet_5.bms` | 19.99 ms | 1.405 ms | 14.23x | 7.82 MB | 647.06 KB | 约 91.9% |

结论：

- 这组 P0 优化把 BMS fixture 的解析时间压到原来的约 8% 到 16%，确认 `lowerEntry/descendingMap` 的全量排序是首要热点。
- 分配量同步下降到原来的约 7% 到 10%，说明原 LINQ 排序和临时集合是主要内存来源之一。
- `SortedList.Add` 理论上是 O(n)，比 `SortedDictionary.Add` 的 O(log n) 插入更贵；但当前 workload 的主要成本是大量 predecessor 查询和反向遍历，实测收益显著覆盖插入成本。

### 解析结果一致性验证

为确认优化没有改变解析结果，本次额外做了旧版与优化版的模型摘要比对：

1. 用单独 `git worktree` 从 `HEAD` 构建未优化 baseline。
2. 用当前工作树构建优化版。
3. 对同一组 4 个 benchmark fixture 调用 `BMSDecoder.decode(byte[], false, null, Shift-JIS)`。
4. 输出稳定模型摘要并逐行比较。

摘要覆盖内容：

- metadata：title、artist、BPM、mode、MD5、SHA256、stagefile、banner、preview 等。
- wav/bga 映射表。
- 每条 timeline 的 section、micro/milli time、BPM、STOP、scroll、section line、BGA、layer、event layer。
- lane note、hidden note、BGM note。
- `LongNote` 的 type、end 标记、pair section/time/wav。
- `MineNote` damage 与 layered notes。

比对结果：

| 项目 | Baseline | P0 后 |
| --- | --- | --- |
| 模型摘要 SHA256 | `8702C769D65B1089F9837D30CBE312962ACD23591C8B7B27FF8275B4F34F264D` | `8702C769D65B1089F9837D30CBE312962ACD23591C8B7B27FF8275B4F34F264D` |
| 摘要行数 | 29085 | 29085 |
| 逐行比较 | 一致 | 一致 |

结论：在 `_ANOTHER.bms`、`AF_7KSHD.bms`、`Lapras_A.bml`、`tetrinet_5.bms` 这 4 个 BMS/BML fixture 上，优化前后解析出的模型内容一致。

### 影响面与副作用

- BMS/BML 的 timeline 创建、BPM/STOP/SCROLL 时间继承、LNOBJ/LN 反向扫描路径都会受益于 `SortedList` 和新的 `lowerEntry/descendingMap`。
- BMSON 的 timeline cache 已切到同一快路径，但本次 benchmark 与解析等价性验证没有 BMSON fixture；BMSON 需要补充独立样本后再确认收益和行为。
- `SortedList` 对乱序大量插入不如 `SortedDictionary` 稳定。如果遇到极端谱面导致 timeline 插入高度乱序，需要重新评估插入成本。
- BMS timeline key 仍是 `double section`，本次未改变浮点比较语义，也未修复潜在精度边界。
- `subMap` 仍会返回新 `Dictionary`，还不是无分配 range iterator；这是后续 BMSON/LN 优化点。
- 本次验证未覆盖 `#RANDOM` 多分支随机选择场景，也未覆盖 `bug.bms` 这种超大谱面。
