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
