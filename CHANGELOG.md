# 更新记录

## 1.9.1（2026-11-23）

### 新增功能与界面优化
- **在线选人 & `Alt+K` 原地联动**：顶栏新增「在线选人」入口，同步挤服收藏夹服务器名单，支持拼音搜索、隐藏 Bot、3 种排序与「查找我在哪个服」；`Alt+K` 快捷搜索支持直接输入 **`@`**、点击目标胶囊或按 `Ctrl+T` 原地切换在线玩家列表，设为目标后立即退回指令检索；目标名含分号 `;` 时自动加引号防止控制台误拆句（[OnlinePlayerPicker.cs](src/OnlinePlayerPicker.cs) / [CommandPalette.cs](src/CommandPalette.cs) / [MainForm.Navigation.cs](src/MainForm.Navigation.cs)）。
- **独立「设置」页（管理版）**：左侧导航栏底部新增「设置」页（[MainForm.Settings.cs](src/MainForm.Settings.cs)），将界面主题/侧栏折叠/关闭缩进托盘、`Alt+K` 位置/透明度/选人偏好、全局热键、数据更新与备份、日志占用查看与一键清空集中收拢并与原快捷入口双向实时同步；「关于与更新」同步精简并新增「更多设置…」快捷跳转，导航栏新增紧凑高度自适应防重叠缩放（[ModernControls.cs](src/ModernControls.cs) / [Theme.cs](src/Theme.cs)）。
- **屏幕准星方案卡片与可选收进托盘**：准星页新增可视化方案卡片条（4 套默认/最多 6 套自定义、支持保存覆盖/新建/右键重命名与删除）及中心位置 `±20px` 微调（[Overlay.cs](src/Overlay.cs) / [MainForm.JoinAndOcr.cs](src/MainForm.JoinAndOcr.cs)）；支持可选「点关闭 `(×)` 缩进系统托盘」与重复启动单实例唤醒（不销毁窗口句柄、不掉热键）。
- **纯说明卡片分层 & 文案精简**：数据表中不可展开的纯解释性条目升级为暖琥珀圆角提示卡片 + `说明` 胶囊徽标，与蓝色可折叠分类头清晰区隔（[Widgets.cs](src/Widgets.cs)）；全量精简新增界面的技术术语，并修复 `Alt+K` 右下角透明度胶囊省略号截断。

### 底层安全与稳定性加固
- **性能与安全**：OCR 改用 `SoftwareBitmap.LockBuffer` 零 PNG 内存直传与 `TaskCompletionSource` 异步等待；更新下载禁止 HTTPS 降级跳转、限制下载体积并在生产默认拒绝 `file://`；`MiniJson` 严格校验字面量；`HubKey` 支持本地 DPAPI 加密持久化（`HubKeyEnc`），备份脱敏同时剔除明文/密文密钥；新增按来源限流的 `Native.LogWarn`、`GetLogSizeBytes()` / `ClearLog()` 与退出时释放 `Overlay`；管理版自检增至 **121 项**。

---

## 1.9.0（2026-11-19）

### 界面架构与效率革新
- **管理版工作台重构**：告别顶部拥挤的 10 个 Tab，升级为**可折叠左侧导航轨**（[NavRail](src/ModernControls.cs)）+ **顶部全局目标玩家栏**（跨 6 张表实时同步并记忆最近 8 个目标）+ **底部交互状态胶囊栏**（[StatusPillBar](src/ModernControls.cs)，点击胶囊直切准星/跳页/打开热键设置）。将「挤服助手」「屏幕准星」「倒计时」提升为一级直达页签，并将数据表底部预览压缩为 `44px` 按需展开代码栏，首屏可视高度提升近 **45%**（[MainForm.Navigation.cs](src/MainForm.Navigation.cs) / [MainForm.Data.cs](src/MainForm.Data.cs)）。
- **全局命令面板（`Alt+K` 全局热键与顶栏「呼出全局搜索弹窗」）**：支持**居中 / 左侧 / 右侧三档悬浮覆盖层**（侧边模式自动丝滑滑出并切换为完整命令的双行卡片，再按一次 `Alt+K` 自动收起并切回游戏焦点）与半透明调节（默认 `86%`，支持点击底栏胶囊与 `Ctrl+滚轮` 微调并持久化）及 Win11 硬件圆角；内置纯 C# 智能拼音引擎（支持全拼/首字母/混拼如 `xrq`、`jiqiren`、`jiqr`，含 `zh/ch/sh` 双字母声母、常见多音字、GBK 二级/繁体游戏字与系统兜底），支持跨 6 张数据表 + 自定义文案多词搜索、星标置顶与最近复制排序、快捷 `Enter`/`Shift+Enter`（`ulx`）复制（[CommandPalette.cs](src/CommandPalette.cs)）。
- **常用收藏汇聚与折叠列表性能优化**：数据表左侧新增 `★` 收藏开关（持久化至 `pins.txt` 并纳入 `.rxbak` 备份），右侧命令改用圆角等宽代码胶囊（Code Chip），支持搜索命中关键词局部高亮；同步重构 `WrapList` 为固定滚动条槽位、按需懒测与局部重绘，彻底解决分类展开/收起跨越视口高度时的掉帧卡顿（[Widgets.cs](src/Widgets.cs) / [Theme.cs](src/Theme.cs)）。

### 游戏内 HUD、可视化与工作流
- **真 32-bit 每像素透明 HUD（`UpdateLayeredWindow`）**：彻底淘汰洋红扣色与锯齿边缘——矢量准星支持抗锯齿、0~2px 黑色外描边与透明度滑块；自定义 PNG 完整保留 8-bit Alpha 半透明光晕；预览框右键支持暗底/亮底/棋盘格切换；倒计时默认升级为高清黑边纯数字，并可选开启半透明胶囊背板与进度条，同时修复低频呼吸脉冲掉帧问题；`Toast` 升级为带状态灯的圆角浮层（[Native.cs](src/Native.cs) / [Overlay.cs](src/Overlay.cs)）。
- **可拖动倒计时时间轴 & A2S 雷达增强**：倒计时页顶部新增横向交互时间轴画布（[TimelineCanvas](src/ModernControls.cs)），支持**直接点击/拖动轨道调整倒计时进度**并实时预览变色/闪烁/事件触发，同时支持标钉悬停查看与点击定位；挤服助手 A2S 解析出**当前地图**、**实时延迟（Ping ms）**并在人数徽标底部绘制容量进度条，支持按地图/延迟排序（[MainForm.ServerList.cs](src/MainForm.ServerList.cs) / [MainForm.JoinAndOcr.cs](src/MainForm.JoinAndOcr.cs)）。
- **现代自定义卡片 & 安全性/Bug 修复**：自定义文案改用左色条 + 两行正文预览卡片（[CustomPhraseCard](src/ModernControls.cs)），支持 `{target}` 动态目标与多行右键 `; ` 控制台串联复制（[MainForm.Custom.cs](src/MainForm.Custom.cs)）；「关闭热键」按钮新增游戏原生危险快捷键误触警告提示；修复热键关闭态改键误重开全局热键及右键复制替换命令漏替换目标 ID 问题（[MainForm.Hotkeys.cs](src/MainForm.Hotkeys.cs) / [MainForm.Data.cs](src/MainForm.Data.cs)）。

---

## 1.8.3（2026-11-19）

### 安全与更新体验
- **在线更新加固与进度可视**：默认更新地址及跳转链接强制校验 HTTPS（本地回环调试地址除外）；支持下载百分比实时进度条，系统代理不可达时自动尝试直连回退；`.old` 旧文件改为重启后延时/启动清理并支持异常回退（[MainForm.About.cs](src/MainForm.About.cs) / [program.cs](build/program.cs)）。
- **备份脱敏与防误覆盖**：`.rxbak` 备份导入/导出自动对 `HubKey=` 进行脱敏处理，避免误传泄漏密钥；应用云端数据表前自动创建本地备份（`ApplyDataOverrides`），且仅落盘存在实际差异的条目（[MainForm.DataBackup.cs](src/MainForm.DataBackup.cs) / [MainForm.Data.cs](src/MainForm.Data.cs)）。

### 体验优化与修复
- **表格编辑防护与对比精准化**：`EditDataTab` 基于临时副本编辑，提供保存/取消明确确认及单元格文本规范化；`ComputeTabDiff` 与备份对比限定在各自段名范围内，列表摘要解析改为高性能流式读取（[MainForm.DevHub.cs](src/MainForm.DevHub.cs) / [MainForm.DataBackup.cs](src/MainForm.DataBackup.cs)）。
- **多屏定位与输入法清理**：屏幕准星/倒计时支持按游戏窗口所在显示器坐标精准定位（`GetTargetScreenBounds`），副屏也能居中对齐；切英文输入法时若临时加载了系统英文布局，退出发送时自动卸载还原，不残留多余输入法（[Overlay.cs](src/Overlay.cs) / [Native.cs](src/Native.cs) / [MainForm.JoinAndOcr.cs](src/MainForm.JoinAndOcr.cs)）。
- **服务端与发布工程化**：Worker 鉴权增加缓存与写入限流（[build/pages/_worker.js](build/pages/_worker.js)），开发者提交接口清晰透传 4xx/5xx 错因；新增 `build/verify.ps1` 与 `build/local_update_test.py` 一键回归及本地模拟自更新脚本（管理版自检增至 **87 项**）。

---

## 1.8.2（2026-11-17）

### 新增与优化
- **分类表默认收起与一键展开/收起**：启动时各数据表可折叠分组默认全收起；单次运行期间切换标签页、切换窗口、切换写法或填写目标 ID 均保留你手动调整的折叠状态；搜索区下方新增「全部展开 / 全部收起」联动切换按钮（[Widgets.cs](src/Widgets.cs) / [MainForm.Data.cs](src/MainForm.Data.cs)）。
- **悬浮窗前台切换自动补置顶**：屏幕准星与倒计时悬浮窗检测到前台窗口切换后延迟 `220ms` 自动补齐 `MakeTopmost`（配合 2 秒轻量兜底，`SWP_NOACTIVATE` 不抢游戏焦点），解决从桌面切回游戏或配合全屏缩放工具时悬浮窗被遮挡的问题（[MainForm.Core.cs](src/MainForm.Core.cs)）。

### 修复与数据同步
- **修复「上传到服务器」偶发 `bad sha256` 失败**：`JsonEsc` 对 `< ' '` 控制字符改用标准 `\u00xx` 无损转义（不再替换为 `' '` 导致与本地原文哈希不一致），同时 `DevCell` 在导出/提交快照时清理粘贴带入的隐藏控制字符（自检增至 **79 项**）。
- **数据表更新**：合入合作开发者实测修订（物品表枪械栏 `L85` 替换为 `cw_kk_ins2_cstm_l85`、`127` 标题修正为 `血肉枪/SCP-122`、技能表 `SCP-5000模式（5K）` 标题调整）。

---

## 1.8.1（2026-11-17）

### 修复与调整
- **修复 `WrapList` 快速连点多分类标题失效**：禁用 `ControlStyles.StandardDoubleClick` 避免 500ms 内连续点击被判定为双击吞掉，并将可折叠标题切换提前到 `OnMouseDown` 即时触发，消除上方分组收起动画位移导致 `OnMouseClick` 点偏的问题（管理版自检增至 **78 项**）。
- **补充管理版识别后缀**：主窗口标题、`关于` 页主标题以及 `exe` 程序集属性（`AssemblyTitle` / `AssemblyProduct` / `AssemblyDescription`）统一增加 `（管理版）` 标识，与玩家版明确区分。
- **规范开源与免费声明**：将 [MainForm.About.cs](src/MainForm.About.cs) 与 [使用说明.txt](docs/使用说明.txt) 中的授权与开源措辞调整为标准 MIT License 表达，并明确提示请勿从任何付费或倒卖渠道购买。

---

## 1.8.0（2026-11-17）

> [!IMPORTANT]
> **重要更新提醒（升级用户必读）**
> 1. **主程序文件名固定为 `Rx工具箱.exe`**：自本版本起取消文件名中的版本号后缀（由原 `RX工具箱 V1.7.exe` 改名为 `Rx工具箱.exe`），今后升级版本号也不再改名。**此次更新完成后，原有的“V1.7”桌面快捷方式会失效**，请删除旧快捷方式并右键 `Rx工具箱.exe` 重新创建，或手动修改原快捷方式的目标路径。
> 2. **曾自定义过指令/物品表的用户请核对数据与备份**：若你此前在「编辑条目」中修改过条目（目录中存在 `data-custom.txt`），旧版本升级时可能未将官方新版 `phrases.json` 覆盖写入对应列表。1.8.0 起改为仅锁定你亲手修改过的页签，并在「关于 → 数据对比与备份管理…」中支持拉取最新官方表、查看新旧差异明细、按页清除本地覆盖及 `.rxbak` 完整备份/选择性还原，建议更新后点开核对并及时备份。

### 新增与优化
- **指令列表分类折叠**：点击任意含下属条目的分类标题栏（普通分类与 `▓` 一级大类）即可展开/收起，无需额外按钮，带约 150ms 平滑抽屉动画与旋转指示箭头；自动兼容顶部/中间无下属条目的“提示/说明”独立横幅。
- **折叠状态与交互联动**：收起后右侧显示已收起条目数；切写法（主指令 / `devforcespawn` / `ulx`）或填写目标 ID 时保留折叠状态；右上角「分类 ▾」跳转自动展开目标分组；方向键上下选择自动跳过已折叠条目。
- **数据更新自主选择与本地备份还原中心**：
  - 在「关于」页新增「更新时数据表处理」策略选项：**询问我**（默认，先下载校验再由用户选“替换(先自动备份) / 暂存为 `phrases.new.json`”）、**自动替换未改页（保留自改页）**、**仅下载暂存不自动替换**。
  - 修复 `SaveDataOverrides` 过去会一次性把全部 6 张表锁进 `data-custom.txt` 的问题：改为只记录用户真正修改过的页签，未改动页签持续随官方 `phrases.json` 同步更新。
  - 新增「数据对比与本地备份管理」中心（`关于` 页与「编辑条目」均可进入）：支持不升 exe 单独检查/拉取最新官方数据、查看每张表增删改差异明细、清除单页本地修改、手动/自动完整备份为单文件 `.rxbak`（涵盖 `custom.txt` / `data-custom.txt` / `phrases.json` / `settings.ini` / `servers.txt` / 自定义准星图）、导出外部备份文件，以及按勾选部分即时热还原（还原前自动防误操作备份）。

### 架构与安全改进
- **主程序固定命名与旧名平滑迁移**：产物固定为 `Rx工具箱.exe`；从 `RX工具箱 V1.x.exe` 自动更新进入 1.8.0 时，启动后会在同目录内以纯 C# 原地迁移更名为 `Rx工具箱.exe` 并弹出引导提醒。
- **重构自动更新为纯 C# 原地改名替换**：移除 `powershell.exe -EncodedCommand` 与 `%TEMP%\*.exe` 中转，改为同目录 `.upd` 下载与 64 位 SHA256 校验、`.old` 原地改名替换、`--after-update <oldPid>` 进程退出握手清理与启动失败原子回滚保护，减少安全软件误报（自检扩至 **76 项**）。
- **死代码清理**：移除 `Native.cs` 中未使用的 `SendInput` / `SendScan` / `PressKey` 相关 P/Invoke 与结构体定义。

---

## 1.7.5（2026-11-17）

- 数据表与 `ulx` 映射优化；新增屏幕倒计时（时间链）悬浮窗与 `F9`/`Ctrl+F9`/`F10` 热键；三档主题（默认护眼浅色）；自绘滚动条与全量安全审计修复。

---

## 1.6.0（开发中）

规划见 `docs/开发计划-V1.6.md`。开工前先确认该文档第 6 节的三个问题。

### 计划内
- （待填）`rx-core.cs` 拆分
- （待填）死代码清理

### 修复
- （待填）

---

## 1.5.0

外部审计（Gemini）+ 重构改进版合入。审计报告见 `docs/审计报告-V1.4.md`。

### 阻断级修复
- **OCR 自动停止没有复位状态**：只置 `autoOn=false`，`typingOn` 仍为 true → 下次 F6 要按两次、
  按钮显示成"停止中"、且游戏输入法永久卡在英文。现在统一走 `StopActiveJoin()`，一并还原输入法。
- **手动档每次 Tick 提前 return**：游戏在后台时 `lastEnter` 不更新，导致 OCR 与状态栏刷新被永久短路。
- **OCR 跑在 UI 线程**：每 2 秒截屏 + PNG + WinRT 识别同步阻塞 100~400ms；且
  `SoftwareBitmap` / `InMemoryRandomAccessStream` / `DataWriter` 全部没释放，挂机半小时泄漏近千个非托管对象。
  现改为后台线程 `TriggerOcrCheckAsync` + 引擎缓存 + 严格 using。
- **抓键弹窗期间热键兜底误触发**：改键时按 F6 会启动挤服、按 Ctrl+F9 会退出程序。已加 `capturingKey` 守卫，
  并按用户自定义的 `hkVk/hkMod` 动态轮询。
- **挤服中切换模式/增删服务器**：目标列表与实际运行状态脱节；刷新人数的后台回调还会按旧下标写入新列表，
  把真名/人数写串行并持久化。已加 `favVersion` 版本号 + 地址双重校验。
- **刷新人数写错列**：`"查询中…"` 写到第 2 列（服务器名）而不是人数列。

### 重要修复
- 剪贴板：重复 SetText、被锁时静默把玩家原剪贴板（可能是密码）粘进控制台；现在带 3 次重试，
  写入失败跳过本拍，并在启停时备份/还原玩家剪辑板。
- `ToggleTyping` 抢前台时无条件 `SW_RESTORE` 会把最大化窗口打回窗口态；改为仅最小化时才还原。
- 退出时未还原输入法。
- 编辑收藏时无法清空备注名。

### 结构调整
- 删除约 1000 行死代码（旧自动挡、旧收藏下拉、autoexec 绑定、UIA 弹窗扫描）。
- 移除 `UIAutomationClient/Types/WindowsBase` 三个 WPF 程序集引用，启动更快。
- GOC 页用 `#if !NO_GOC` 完整包裹（此前只有调用点被包裹，1500 行仍编进 exe）。
- 字体改为享元缓存池（`fontPool`），反复开弹窗不再增长 GDI 句柄。

### 界面与交互
- 服务器列表：双击缓冲（`BufferedListView`）不闪；点表头排序（含"空位优先"）；只有人数列上色，
  其余列恢复深灰，满员/空位带文字后缀；正在挤的行首显示 ▶。
- 底部新增「备注（可选）」输入框、「粘贴加服」按钮，IP 框/备注框回车即可加入收藏。
- 指令表/模型表：每行右侧直接显示指令预览，不用再逐条点；顶部预览框有空状态提示；
  筛选支持空格分词多关键词。
- 移除 `lblGuide` 幽灵空白行，列表多出一整行高度。
- 热键改动后，按钮、手动档引导、准星说明、使用指南里的键名全部联动更新。

### 未采纳
- 审计方附带的 `src/split/`（6 个 partial class 拆分文件）**无法编译**：拆分点落在方法体中间，
  `rx-form-goc.cs` 第 34 行起就是构造函数语句直接挂在类里。本次只合入可编译的单文件版本，
  拆分由我们自己按方法边界重做（见下个待办）。

## 1.4.0

### 挤服重做（工具 → 挤服助手）
- 一个开始键 F6 + 三模式单选：手动档 / 单一 IP / 收藏轮询（原 Ctrl+F10 重复键已去掉）
- 服务器列表改为三列 ListView（名字 / 人数 / 地址）：
  · 单击选中 = 单一 IP 的目标；勾选 = 参与收藏轮询
  · 人数列红=满员、绿=有空位、灰=查询失败
- 人数改为手动「刷新人数」一次性并发查询（不再后台常驻轮询）
- 单一 IP 模式隐藏勾选框与全选按钮；手动档隐藏列表、改用图文引导
- 新增「?」使用指南：按当前模式给出①分步操作 ②原理与工作流
- 新增「★ 加入收藏」（自动用 A2S 查服务器名当备注）、「删除选中」
- 模式与列表勾选状态进 settings.ini（JMode）

### 修复
- 手动档恢复可用：去掉 V1.4 早期误留的「必须填地址 / 玩家名」校验
- 底栏与标签页停靠顺序反了，状态栏盖住页面底部（底部提示被裁切）
- 列表默认选中项会被 ListView 焦点逻辑改掉，改为延时校正到第一行

## 1.2.0

### 新增
- 挤服：人数 >=50 时 250ms 高频轮询，人数下降沿立即发起连接（5 秒防抖）
- 挤服：读取 garrysmod\console.log 判断 加载中/已进服/被拒，动态调整重连节奏
- 热键改为 RegisterHotKey 系统级注册，注册失败自动回退轮询
- 底栏状态文字启用 AutoEllipsis，长服务器名不再折行裁切

## 1.2.0 计划（原始）

版本号已约定：下次打包用 1.2（`pwsh -File buildump-version.ps1 1.2` 一键改三处）。

待做三项（已请审计方以补丁形式提供）：

- [ ] 满员时 A2S 轮询提速到 250ms，人数下降沿立即触发 steam://connect
- [ ] 读 `garrysmod/console.log` 判断「加载中 / 已进服 / 连接失败」，避免加载中重复连接卡死
- [ ] F5–F8 改用 RegisterHotKey，被占用时降级为轮询

暂不做（评估后收益/风险不匹配）：

- 准星改用 UpdateLayeredWindow（要重写 Overlay，无法快速验证）
- 卡片虚拟化网格（当前条目数量下收益有限）
- 游戏内 Quick-Bar / cfg 桥接（需改游戏绑定，超出工具范围）

## 1.1.0-dev（进行中）

### 新增
- 挤服分「自动挡 / 手动挡」：
  - 自动挡：循环 `steam://connect/ip:port`，不按键、不抢焦点，Steam 自己启动并连接
  - 手动挡：游戏前台时连发回车（`keybd_event`）
- 服务器收藏夹：`servers.txt`，支持查询名称、按备注名覆盖、下拉显示实时人数
- A2S 进服检测：比对 Steam 昵称判断是否已进服，进服自动停止
- 工具页拆分为「挤服 / 准星」两个子页
- 准星「同心圆」样式；V 形样式取代实心倒三角；线宽/臂长/间隙可调
- 列表页：长条目自动换行 + 行间分割线 + 像素级滚动（自绘 WrapList）
- 自定义页：卡片式一键复制 + 悬停预览 + 编辑条目

### 修复
- 自绘列表重复绘制 / 滚动方向错乱
- `steam://connect` 在 .NET 8 下因未启用 ShellExecute 而失效
- 连接请求过于频繁导致游戏加载卡死（改为未启动 15s / 加载中 45s 冷却）

### 修复（外部审计，第二轮）
- [x] 三个网络查询移到后台线程（Task.Run + BeginInvoke），UI 不再冻结
- [x] A2S 分包重组：剥离 12 字节分包头、按序号拼接、收齐即返回
- [x] A2S 包头校验（0xFFFFFFFF + 类型字节）与回包源 IP 校验
- [x] 支持域名地址（Dns 解析），不再要求纯 IP
- [x] GOC 推荐：候选 > 4000 时跳过 O(n²) Minimax，避免 6 位题卡死
- [x] Font / ToolTip / 右键菜单 / Toast 定时器释放
- [x] custom.txt 控制字符清洗 + 原子写（File.Replace，失败回退覆盖移动）
- [x] servers.txt 备注名中的 | 自动替换；读写失败写日志而非静默
- [x] 筛选后重置选中（不再错位选中）
- [x] ParseAddr 主机字符白名单，LaunchUrl 返回成败
- [x] 单实例 Mutex（普通版 / 内参版各一把，可同时运行）
- [x] 清理死代码：EnsureForeground / PostEnter / ShowCopyMenu / ColorsMatch / TotalMax
- [x] 准星提示文案改为「6色循环」，退出热键改为 Ctrl+F9


### 第三轮（旗舰模型审计后重整）
以该包为基底合并，补回被其删除的免责声明，并逐页实测：

- A2S 玩家名改为按 0x44 报文逐条精确匹配（修掉 BigCat 误判 Cat）
- GOC 推荐改用位掩码 FeedbackKey，1296 候选从数百毫秒降到数毫秒
- Interlocked + querySeq 版本号，消除强制查询的并发竞态
- SafeBeginInvoke 统一拦截窗口销毁时的回调异常
- 二元频率表大小写修正（原先全小写，与大写输出永不匹配，评分失效）
- 分包重组校验 4 字节 ID、拦截 BZip2 压缩包、未收齐即丢弃
- Process 对象释放、GOC 控件释放、日志加锁与轮转
- 新增：准星实时预览、进服检测名可编辑、指令表筛选统计与清空、
  卡片右键菜单、设置持久化 settings.ini
- 恢复：底部免责声明

### 明确拒绝的改动
- 不启用 SetProcessDPIAware：125% 缩放下会导致控件错位（已实测）
- GOC 阈值不采用 1200：默认 6×4=1296 会误入退化分支，改为 4000

## 1.0.0

首个发布版：指令表 / 模型表 / 职业表 / SCP角色 / 自定义 / GOC奇术小抄 / 工具
