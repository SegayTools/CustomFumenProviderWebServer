# 谱面上传/修改：谱面文件归档与 CREATOR 处理

- 状态：已实现并通过测试
- 更新时间：2026-10-30
- 代码基线：`CustomFumenProviderWebServer`（.NET 8 / ASP.NET Core）；游戏端判定参考 `D:\sddt160`
- 本文档记录需求、代码事实、问答结论与实现结果。

## 1. 需求（R）

| ID | 需求 | 状态 |
|---|---|---|
| R-1 | 谱面上传或修改时，用户提供的谱面文件都要**改名**存放到该 musicId 的谱面文件夹根目录。例：musicId `25666` 的 Master 谱 → `/files/fumen25666/master.ogkr` | 已实现 |
| R-2 | 同时生成一份**已处理**的谱面文件到 `/files/fumen25666/opt/music/music25666/25666_03.ogkr` | 已实现 |
| R-3 | “处理”指改写 CREATOR 字段为 `[自制谱 Lv.<难度>] <原谱师>`；难度取 Music.xml 的定数 | 已实现 |
| R-4 | 全部内容写入 `docs/` 本文档 | 已完成 |
| R-5 | 上传时检查 CREATOR 是否存在且非空白；缺失/空白按默认值 `-` 处理 | 已实现 |
| R-6 | CREATOR 改写必须可开关，**默认关闭**；关闭时归档与 opt 副本都保持上传原文 | 已实现 |

## 2. 代码事实（F）

| ID | 事实 | 依据 |
|---|---|---|
| F-1 | 存储根目录来自环境变量 `FumenDirectory`；`/files` 静态映射该目录，且开启目录浏览，未知扩展名可下载 | `Program.cs:76-93` |
| F-2 | 每个谱面目录为 `fumen{musicId:D4}`，内含 `opt/`、`fumen{id:D4}.zip`、`info.json`、`jacket.png` | `FumenStorageService.cs` |
| F-3 | 游戏读取的权威谱面路径是 `opt/music/music{id:D4}/{id:D4}_0{diffIdx}.ogkr`；`diffIdx` 0..4 = Basic/Advanced/Expert/Master/Lunatic，`_03` = Master | `FumenStorageService.cs`、`MusicXmlService.cs` |
| F-4 | 首次投稿 `POST /api/deliverFumen`：五个可选谱面先落临时文件，musicId 重分配为 `+20000`，再写入 opt 并打包 zip | `FumenController.Public.cs` |
| F-5 | 单谱面修改 `POST /api/update/ogkr`：`ogkrFormFile / diffIdx(0..4) / musicId / password / changeDiff?`，有整目录+DB 回滚保护 | `FumenController.Public.cs`、`FumenUpdateScope.cs` |
| F-6 | 定数解析：`Level = FumenConstIntegerPart + FumenConstFractionalPart/100` | `MusicXmlService.cs` |
| F-7 | 改动前只有读取 CREATOR，没有重写谱面内容的代码 | `MusicXmlService.cs`（改动前） |
| F-8 | `UpdateSetInfo` 会用磁盘上的 opt 谱面重新解析并写库，因此改写后必须剥离前缀，否则 DB 会存入 `[自制谱 ...]` | `FumenController.Public.cs` |
| F-9 | `update/xml` 会重算定数并重写 Music.xml；`changeDiff` 也会改定数 | `FumenController.Public.cs` |
| F-10 | zip 只打包 `opt/`；`/fileCache/list` 只枚举 `opt/`；根目录归档不进两者，但可经 `/files` 访问 | `FumenController.Public.cs`、`FileCacheListService.cs` |
| F-11 | 重新投稿会先删除 `fumen{id}` 根目录文件，再复制生成内容 | `FumenController.Public.cs` |
| F-12 | **游戏端 `+` 判定**：`isFumenConstPlus = FumenConstFractionalPart >= fumenLevelPlusBorder`，`fumenLevelPlusBorder = 70` 硬编码。即 `13.70–13.99` 显示 `13+`，`13.00–13.69` 显示 `13` | `D:\sddt160\MU3\Data\DataManager.cs:599`、`MU3\Data\MusicData.cs:126`、`MU3\Data\GameData.cs:552-577` |
| F-13 | **游戏端谱面格式**：逐行读取，行内按 `\t` 分列，首列为 tag；`CREATOR` 是 header tag，读取后显示为谱师名 | `D:\sddt160\MU3\Reader\Record.cs:84-118`、`Reader\Header.cs:87`、`Data\DataManager.cs:1753` |
| F-14 | **游戏端等级显示**：等级数字与 `+` 是独立对象，显示 = 整数部分 + 条件性 `+` | `D:\sddt160\MU3\SceneObject\MusicDataObject.cs:117-124` |

## 3. 路径约定（T）

| ID | 用途 | 路径（相对 `/files`） |
|---|---|---|
| T-1 | 原始谱面归档 | `fumen25666/master.ogkr`（其余：`basic/advanced/expert/lunatic.ogkr`） |
| T-2 | 游戏读取的处理后谱面 | `fumen25666/opt/music/music25666/25666_03.ogkr` |
| T-3 | 下载包 | `fumen25666/fumen25666.zip`（仅含 `opt/`，归档不进 zip） |
| T-4 | 元数据 | `fumen25666/info.json`、`jacket.png` |

难度映射：

| diffIdx | 名称 | opt 文件名 | 归档文件名 | 定数 `+` 边界 |
|---|---|---|---|---|
| 0 | Basic | `25666_00.ogkr` | `basic.ogkr` | `>= .70` |
| 1 | Advanced | `25666_01.ogkr` | `advanced.ogkr` | `>= .70` |
| 2 | Expert | `25666_02.ogkr` | `expert.ogkr` | `>= .70` |
| 3 | Master | `25666_03.ogkr` | `master.ogkr` | `>= .70` |
| 4 | Lunatic | `25666_04.ogkr` | `lunatic.ogkr` | `>= .70` |

## 4. 问答结论（Q → D）

| ID | 问题 | 结论 |
|---|---|---|
| Q-1 | 归档命名 | `basic/advanced/expert/master/lunatic.ogkr`，固定小写 `.ogkr` |
| Q-2 | opt 路径层级 | 以 `opt/music/music{id}/{id}_0d.ogkr` 为准（需求原文少一层 `music/` 视为简写） |
| Q-3 | 归档内容 | 保持上传字节原样，CREATOR 只在 opt 副本改写 |
| Q-4 | `+` 规则 | 按游戏代码：小数部分 `>= 70` 加 `+` |
| Q-5 | 空 CREATOR | 默认值 `-`；示例中的 `DJ.MSDP` 是上传谱面 CREATOR 的真实值 |
| Q-6 | DB `Creator` | 存原谱师；解析时剥离 `[自制谱 Lv.*]` 前缀 |
| Q-7 | 重算触发 | `deliverFumen`、`update/ogkr`（含 `changeDiff`）、`update/xml`；定数变化即重算 |
| Q-8 | 幂等/保真 | 先剥离旧前缀再套新前缀；只改第一处 CREATOR；保留分隔符/换行/编码/BOM |
| Q-9 | zip/缓存清单 | 归档都不进，仅 `/files` 直接访问 |
| Q-10 | 存量回填 | 暂不处理 |
| Q-11/Q-12 | CREATOR 校验 | 检查存在且非空白；缺失/空白用 `-` 继续处理，不拒绝上传 |
| Q-13 | 无法安全改写 | 非合法 UTF-8 或找不到任何头部 tag → 请求失败并回滚，不写坏文件 |
| Q-14 | 改写开关 | 新增环境变量 `EnableCreatorRewrite`，默认 `false`；开启后才改写 CREATOR，关闭时行为与改动前一致（归档仍保留） |

## 5. 实现（I）

| ID | 文件 | 改动 |
|---|---|---|
| I-1 | `Utils/FumenDifficultNames.cs`（新增） | 难度下标 → 显示名 / 归档文件名唯一映射 |
| I-2 | `Services/FumenChartService.cs`（新增） | 归档原始谱面；改写 CREATOR；定数文本格式化；幂等剥离前缀；UTF-8/BOM 保真与原子写入；单难度/投稿批量落盘 |
| I-3 | `Services/FumenStorageService.cs` | 新增 `GetOptMusicFolder`、`GetOptFumenPath`、`GetArchivePath` |
| I-4 | `Controllers/FumenController.Public.cs` | `DeliverFumen` 归档 + 处理副本；`update/ogkr` 归档 + 处理 + 定数校正；`update/xml` 后重算所有已存在难度 |
| I-5 | `Services/MusicXmlService.cs` | CREATOR 解析锚定 `^CREATOR`，剥离自制谱前缀，空白回退 `-` |
| I-6 | `Models/Tables/FumenDifficult.cs` | `DifficultName` 复用统一映射 |
| I-7 | `Program.cs` | 注册 `FumenChartService` |
| I-8 | `Services/FumenChartService.cs` | 新增 `EnableCreatorRewrite` 开关：默认关闭；关闭时归档/副本均原样复制，`RegenerateOptChartAsync` 不触碰文件 |

行为细节：

1. 处理后的 CREATOR 形如 `[自制谱 Lv.13+] DJ.MSDP`；原 CREATOR 为空时为 `[自制谱 Lv.13+] -`。
2. 定数显示：`13.69 → 13`，`13.70 → 13+`（与游戏端 `fumenLevelPlusBorder = 70` 一致）。
3. 缺少 CREATOR 行时插入到 `VERSION` 之后；缺少 `VERSION` 时依次尝试 `BPM_DEF`/`MET_DEF`；都没有则失败。
4. 只替换元数据区第一处 CREATOR；保留 Tab 分隔、CRLF/LF、UTF-8 BOM。
5. `update/ogkr` 先按请求定数生成副本，`UpdateSetInfo` 后按库里最终定数校正，保证 `Lv.` 与 Music.xml 一致。
6. 旧谱面没有根目录归档时，重算以现有 opt 文件为源，仍能安全剥离旧前缀。
7. 归档文件不进入 `fumen{id}.zip` 与 `/fileCache/list`。
8. 开关 `EnableCreatorRewrite`：默认关闭（未设置/无法解析为 `true` 均视为关闭），启动时读取；关闭时不解析/不改写，归档与 opt 副本都是上传原文。

## 6. 验证（V）

全量测试：`dotnet test` → **118 通过，0 失败**。

| ID | 测试 | 覆盖 |
|---|---|---|
| V-1 | `FumenChartServiceTests.FormatLevel_*` | `13.69/13.70` 边界、`+` 规则 |
| V-2 | `FumenChartServiceTests.ProcessToFile_RewritesCreator_*` | CREATOR 改写、BOM、CRLF、其余行不变 |
| V-3 | `FumenChartServiceTests.ProcessToFile_EmptyCreator_*` / `MissingCreator_*` | 默认 `-`、缺行插入 |
| V-4 | `FumenChartServiceTests.ProcessToFile_ExistingPrefix_*` | 重复处理不叠加 |
| V-5 | `FumenChartServiceTests.ProcessToFile_OnlyFirstCreatorLineIsChanged` | 只改第一处 |
| V-6 | `FumenChartServiceTests.ProcessToFile_NonUtf8_*` / `UnrecognizableChart_*` | 失败且不产生目标文件 |
| V-7 | `FumenChartServiceTests.SaveDeliverCharts_*` | 投稿布局：根目录 5 个归档 + opt 5 个处理副本 |
| V-8 | `FumenChartServiceTests.RegenerateOptChart_*` | 优先归档、旧谱面回退 opt |
| V-9 | `FumenControllerTests.UpdateOgkrFumen_*`（2 个） | 真实控制器链路（SQLite）：归档、opt 副本、DB 原谱师、zip 内处理副本、Music.xml 定数；`changeDiff` 与沿用现定数两条路径 |
| V-10 | `MusicXmlServiceTests.*` | 解析剥离前缀、空白回退、`CREATOR_NOT_A_TAG` 不误匹配 |
| V-11 | `FumenControllerTests.ChartServiceSwitch_*` | 默认关闭、`EnableCreatorRewrite=true` 时开启 |
| V-12 | `FumenChartServiceTests.*WhenRewriteDisabled*` | 关闭时字节级原样复制、归档=副本、重算不触碰文件 |
| V-13 | 现有测试文件 | 全量回归 |

说明：未启动完整服务与 MySQL/HTTP 上传（需外部音视频工具与数据库）；投稿链路的谱面落盘逻辑已抽到 `SaveDeliverChartsAsync` 并直接测试，`update/ogkr` 通过控制器级测试覆盖。

## 7. 风险（X）

| ID | 内容 |
|---|---|
| X-1 | `.ogkr` 实际编码仍以 UTF-8（含/不含 BOM）为支持范围；非 UTF-8 谱面会被拒绝 |
| X-2 | 根目录归档与 opt 副本双写，磁盘占用约增加一份谱面；单文件体积小 |
| X-3 | 存量谱面无根目录归档（按要求不回填）；后续 `update/xml` 会以现有 opt 文件为源重算 |
| X-4 | 游戏端 `+` 边界来自 `D:\sddt160` 的硬编码 70；若客户端换版本，需要同步 `LevelPlusBorder` |

## 8. opt / zip / fileCache 同步审计

### 8.1 opt 内容的生成来源

| 来源 | 写入 opt 的内容 |
|---|---|
| `deliverFumen` | 临时生成完整树：`Music.xml`、5 个 `{id}_0d.ogkr`、`assets/*`、`musicsource/*`，打包 zip 后整体复制到 `fumen{id}` |
| `update/jacket` | `opt/assets/{id}.png`、`{id}_s.png`，同时更新根目录 `jacket.png` |
| `update/audio` | `opt/musicsource/musicsource{id}/*.acb|*.awb|MusicSource.xml` |
| `update/xml` | `opt/music/music{id}/Music.xml`，并按库内定数重写所有已存在难度的谱面 CREATOR |
| `update/ogkr` | 根目录 `{难度}.ogkr` 归档 + `opt/music/music{id}/{id}_0d.ogkr` 处理副本 |
| `update/info` | 可选重生成 `opt/.../Music.xml`；缺失 opt 时先从 zip 解包 |
| `update/repackZip` | 不写 opt，仅按当前 opt 重打包 |
| `fumen/update/file` | 管理员指定相对路径，可写 opt 内外任意文件 |

### 8.2 各接口的同步动作

| 接口 | opt 写入 | zip 重打包 | DB / info.json | fileCache |
|---|---|---|---|---|
| `deliverFumen` | 全量生成 | 生成时打包（来自临时目录） | 插入 set + 写 info.json | 无直接刷新；watcher 可能命中 |
| `update/jacket` | 是 | 每次 | `UpdateStateAsync` | 无直接刷新；watcher |
| `update/audio` | 是 | 每次 | `UpdateStateAsync` | 无直接刷新；watcher |
| `update/xml` | 是 | 每次 | `UpdateSetInfo` + `UpdateStateAsync` | 无直接刷新；watcher |
| `update/ogkr` | 是 | 每次 | `UpdateSetInfo` + `UpdateStateAsync` | 无直接刷新；watcher |
| `update/info` | 可能 | 仅 `IsZipOutdated` | `UpdateSetInfo` | 无直接刷新；watcher |
| `update/repackZip` | 否 | 每次 | `UpdateStateAsync` | 无直接刷新；1 分钟扫描 |
| `fumen/update/file` | 任意路径 | 仅 `repackZip=true` | 仅 `repackZip=true` 时 `UpdateSetInfo` | 仅路径位于 `opt/` 下时 watcher |
| `publishFumen` | 否 | 否 | 状态 + 时间 + info.json | 无直接刷新；1 分钟扫描 |
| `readyForPending` | 否 | 否 | 状态 + 时间 + info.json | 无直接刷新；1 分钟扫描 |
| `remove` | 删除整个目录 | — | 删除行 | 立即 `TryRemove` |

### 8.3 结论

1. **专用更新接口（jacket/audio/xml/ogkr/repackZip）成功返回前都会重打包 zip**，zip 是当前 opt 的快照；`update/info` 用 `IsZipOutdated` 条件重打包。
2. **`fumen/update/file` 是主要例外**：默认 `repackZip=false`，写完 opt 后 zip 仍是旧的；且没有 `FumenUpdateScope`，写后解析失败无法回滚。若路径不在 `opt/` 下，zip 本身不会包含该文件。
3. **重新投稿存在 opt 残留**：`deliverFumen` 更新同 musicId 时只删除根目录文件，`opt` 通过 `CopyDirectory` 合并，旧难度/旧资源文件可能保留；而 zip 来自新生成的临时树，因此“存储 opt”可能包含 zip 中没有的文件，fileCache 也会枚举到它们。
4. **fileCache 不是同步通知**：服务端没有推送；客户端轮询 `/fileCache/list`。缓存由 `FileSystemWatcher`（`/fumen{id}/opt/` 文件事件）异步刷新，另有 1 分钟 `ScanChanges` 兜底。
5. **DB 变更与缓存存在窗口期**：`publishFumen`、`readyForPending`、`update/repackZip`、`update/info` 不直接刷新缓存；纯元数据变化最多等 1 分钟。`deliverFumen` 在事务提交前写文件，watcher 可能读不到未提交的 set；且 `ScanChanges` 会排除/移除 `NotReadyForPending`，新投稿要到状态变化后的下一次扫描才稳定进入缓存。
6. **崩溃/失败一致性**：`Process*ToFile` 多处使用非原子 `File.Copy`；`FumenUpdateScope` 只覆盖异常回滚，不覆盖进程崩溃/断电。`deliverFumen` 复制存储阶段若中途失败，只回滚数据库事务，已复制的部分文件不会清理。
7. **缓存范围**：`CacheFumenInfo` 只枚举 `opt/`；zip、`info.json`、`jacket.png`、根目录归档都不在文件清单内，`UpdateTime` 是唯一的元数据信号。

### 8.4 建议（A）

| ID | 建议 |
|---|---|
| A-1 | `update/file` 纳入 `FumenUpdateScope`；写入路径位于 `opt/` 下时默认自动重打包（或拒绝 `repackZip=false`） |
| A-2 | 更新/发布/就绪接口提交后显式调用 `fileCacheListService.UpdateCacheFumenInfo(musicId)`，消除 watcher 与 DB 提交的竞态及 1 分钟窗口 |
| A-3 | `deliverFumen` 重投时先清空 `opt` 子树（或删除整个 `fumen{id}` 后重建），避免旧文件残留 |
| A-4 | `update/xml` 清理 DB 中已不存在的难度对应的 opt 谱面，保证 opt 与 `FumenData` 一致 |
| A-5 | 需要“通知”时增加推送（SSE/SignalR/webhook）或缓存版本号/ETag；当前只能轮询 |
| A-6 | 把 `Process*ToFile` 的写入改为临时文件 + 原子移动，降低崩溃导致的半写风险 |
