# RX 工具箱

GMod 服务器「RXSEND / Breach」类玩法用的**指令速查与操作辅助工具（完整管理版）**。
单文件绿色程序：C# 5 + .NET Framework 4.8 + WinForms，零第三方运行时依赖。

> **普通玩家推荐轻量版**：若无需管理指令表，请使用专为玩家整理的开源分支仓库 👉 **[Dauskwn/rx-tools](https://github.com/Dauskwn/rx-tools)**（已将「挤服助手 / 屏幕准星 / 倒计时 / 自定义」直接铺平为顶层主标签页，内置独立公开更新源）。

---

## 功能

- **指令速查与分类折叠**：单击条目直接复制指令；主指令 / 备用写法 / `ulx` 控制台写法三档切换；点击任意含子条目的分类标题即可平滑展开/收起（自动兼容独立提示横幅）
- **目标替换**：命令里的 `^` 自动替换成目标玩家名（名字带空格会自动加引号）
- **搜索与分类目录**：空格分词筛选、Esc 清空、右上角「分类 ▾」自动展开并跳转
- **条目编辑与按页锁定**：内置编辑器仅锁定你真正修改过的页签至 `data-custom.txt`，未改页签继续随官方底表更新
- **数据策略与 `.rxbak` 本地备份**：支持更新前弹窗询问/自动替换/暂存为 `phrases.new.json`，并提供多表差异明细与手动/自动 `.rxbak` 完整快照与选择性还原
- **自动挤服与屏幕倒计时**：A2S 查询服务器状态 + Windows OCR 识别进服；点击穿透倒计时悬浮窗（支持多方案与时间链变色/闪烁/描边标注）
- **外置准星与三档主题**：置顶透明窗口（6 种矢量样式或自定义图片）；护眼浅色 / 跟随系统 / 夜间深色三档主题；8 项可自定义全局热键 + `Ctrl+F10` 一键归还热键
- **纯 C# 原地更新**：同目录 `.upd` 下载与 64 位 SHA256 校验、`.old` 原地改名替换与异常回滚，主程序固定命名为 `Rx工具箱.exe`（后续升级不破坏快捷方式）
- **在线版本库 / 自动更新**：可选，指向你自己的服务端即可

## 构建

只需 Windows + 自带的 .NET Framework 4.8（`csc.exe`），**不需要**装 .NET SDK：

```powershell
powershell -File build\build.ps1
```

产物在 `dist\`。

## 数据格式

`phrases.json`：一个对象，每个键是一张表，值是条目数组。

```json
{
  "指令表": [
    { "title": "▓ 常用命令", "text": "" },
    {
      "title": "强制缴械",
      "text": "!strip ^",
      "ulx": "ulx strip ^",
      "note": "包括技能/特殊装备"
    }
  ]
}
```

| 字段 | 说明 |
|---|---|
| `title` | 条目名；以 `▓` 开头表示大类标题；`text` 为空表示分组标题 |
| `text` | 主指令，点击时复制的默认内容 |
| `alt` | 备用指令（可选，如角色表的 `devforcespawn` 变体） |
| `ulx` | 控制台 ulx 写法（可选；留空则对 `!` 开头的命令自动加 `ulx ` 前缀） |
| `note` | 标题右侧的灰色小字注解 |

程序内部把条目编码成 `标题 \u0001 主指令 [\u0002 备用] [\u0004 ulx] [\u0003 注解]`，
由 `DevSplitItem` / `DevJoinItem` 负责拆装。

## 自检与调试

```powershell
set RX_SELFTEST=1
dist\Rx工具箱.exe        :: 不建窗口跑断言（76 项），结果写 selftest.log，失败退出码 1

set RX_DUMP_UI=1
dist\Rx工具箱.exe        :: 把每个页签的控件坐标写成 dump-ui.log

dist\Rx工具箱.exe --dev  :: 多实例 + 标题带 DEV，方便与正式版同时开
```

## 隐私

本程序不收集任何数据。只有两处会联网，且都需要你主动触发：

- 检查更新：拉取一个 `version.json`
- 在线版本库：上传/下载数据表（地址和密钥自己填）

## Code signing policy

**Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).**

- Committers and reviewers: [@Dauskwn](https://github.com/Dauskwn) (repository owner)
- Approvers: [@Dauskwn](https://github.com/Dauskwn)
- Privacy policy: This program will not transfer any information to other networked systems unless
  specifically requested by the user or the person installing or operating it. The only network
  operations are an optional update check and an optional, user-configured data synchronisation
  endpoint, both triggered explicitly by the user.
- Build: `.github/workflows/build.yml` compiles `src/*.cs` + `build/program.cs` with the `csc.exe`
  bundled with .NET Framework 4.8, runs `RX_SELFTEST=1` verification, then submits the artifact for signing.
- Status: signing application in progress.

## 许可

[MIT](LICENSE)。允许自由使用、修改与再分发（需保留原作者版权及许可声明）。本工具完全免费开源，官方未授权任何收费版本，请勿从付费或倒卖渠道购买。
