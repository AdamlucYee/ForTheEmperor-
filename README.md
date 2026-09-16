![For The Emperor 主视觉](MAIN_IMAGE_URL)

# FOR THE EMPEROR · 桌面战士

**以帝皇之名，肃清你的桌面。**

Windows 桌宠 v0.2 像素版。WPF 透明窗口、八套像素写实装甲精灵、独立战团动作与动态粒子，以及“先攻击动画，再将桌面文件移入回收站”的真实执行链路。

## 项目演示

### 指挥终端与桌宠互动

![指挥终端与桌宠互动](DEMO_IMAGE_1_URL)

### 以帝皇之名处决

![桌面文件处决演示](DEMO_IMAGE_2_URL)

### 八大战团与专属动作

![战团外观与处决效果](DEMO_IMAGE_3_URL)

<!-- 替换上方四处图片链接，并补齐下方“宣传图片来源”表。 -->

## v0.2 更新

- 修复文件已经回收、桌面却残留失效图标的问题：回收成功后发送 `SHCNE_DELETE` 和父目录 `SHCNE_UPDATEDIR`，使用 `SHCNF_PATHW | SHCNF_FLUSH` 确保 Explorer 收到通知；正常启动时也刷新桌面目录，清理先前残留的视图。
- 替换旧版简化矢量小人：每个战团各有一张 16 姿势精灵图，包括待机、跑步循环、蓄力、攻击和恢复。128 像素逻辑网格、最近邻缩放、真实透明背景。
- 八战团采用不同攻击距离、步频、蓄力长度与命中时点。粒子增加弹壳、碎石、尘环、切割火花、焰流、余烬、烟雾、残影和扫描射线。
- 成功删除后，将目标文件的图标绘制为散开的碎片。演练使用虚拟文档，不涉及真实文件。

## 启动

双击 `dist\ForTheEmperor.exe`。已打包成单个 x64 EXE，需要 **64 位 Windows 10/11 + .NET 8 Windows Desktop Runtime**；本次构建所在电脑已经安装该运行时。无需管理员权限，不设开机自启，不联网。

启动后显示指挥面板，战士出现在鼠标所在显示器右下角。先点 **演练处决动画**，可观看完整流程，不操作真实文件。

- 拖动战士：移动位置。
- 单击：显示战团台词；双击：打开指挥面板。
- 右键战士：切换战团、演练、召回、取消、退出。
- 关闭指挥面板：战士继续驻守。双击托盘图标可重新打开。
- 退出：在战士或托盘菜单选择 **退出并移除右键菜单**。

## 处决桌面文件

1. 保持程序运行，并开启“启用文件右键处决”。
2. 右键一个桌面上的普通文件。
3. Windows 11 先点 **显示更多选项**（也可使用 Shift+F10）。
4. 选择带战士图标的 **以帝皇之名处决**。
5. 战士警戒、转向、跑到文件附近、攻击；命中之后才移入回收站。
6. 确认回收成功后显示 `Purge complete.`，然后返回原位置。

完整状态：`Idle → Alert → Turn → Run → Arrive → Attack → Execution → Recover → Return → Idle`。

**取消：** 命中前按 `Ctrl+Alt+Esc`，或从战士/托盘/指挥面板取消。命中后请从 Windows 回收站恢复。全局快捷键若被其他应用占用，可以使用菜单取消。

一次只执行一个文件；忙碌时的新请求会被拒绝，不积压删除任务。执行期间暂不能切换战团、尺寸或拖动。

## 八个战团

| 战团 | 原型动作 |
|---|---|
| 极限战士 | 稳姿瞄准、三连爆弹、后坐力、抛壳、弹着烟尘 |
| 帝国之拳 | 动力拳蓄力、近身重击、地裂冲击波、飞溅碎石 |
| 太空野狼 | 狼皮披肩、低姿冲锋、横向锯切、连续磨削火星 |
| 圣血天使 | 跳包升空、俯冲劈砍、红白刀光、落地尘环 |
| 黑色圣堂 | 十字肩甲、长袍、双手重斩、持续链锯切割 |
| 火蜥蜴 | 鳞片披风、喷火预热、锥形焰流、黑烟与上升余烬 |
| 暗鸦守卫 | 鸟喙头盔、羽披风、残影突刺、交叉爪痕 |
| 钢铁之手 | 机械义肢、扫描锁定、线圈充能、青色能量束与方块分解 |

台词显示为文字气泡；可选声音使用 Windows 系统提示音，没有角色配音。

## 美术来源、参考与版权说明

**本项目的八套角色精灵图集由 AI 根据文字提示生成；美术方向查阅过第三方像素二创作品。它们不是人工逐像素绘制的作品，也不应被表述为“完全从零原创、未参考任何作品”的角色设计。**

### 角色图与特效如何制作

- 最终收录的八张角色图集使用内置 `image_gen`（ImageGen）根据文字提示生成。提示词描述了战团配色、装甲、武器、姿势和像素写实风格，没有指定模仿某位画师的个人风格。
- 最终八张图集的生成调用未附加第三方画师图片作为图生图输入。制作期间的图片编辑尝试仅使用此前 AI 生成的角色图；这些尝试未被选为最终素材。
- 项目未将上述画师的原图、官方游戏模型或提取的游戏贴图打包进程序。**未直接使用原图不等于没有视觉参考，也不等于拥有战锤角色设计的原创权利。**
- 角色移动、精灵切分、姿势播放以及火焰、烟尘、弹壳、火花等动态粒子由项目代码实现；处决时散开的文件图标来自 Windows Shell。
- 最终采用的生成图集保存在 [`assets/sprites/`](assets/sprites/)，生成工具与完整文字提示记录在 [`generation-prompts.json`](assets/sprites/generation-prompts.json)。这些记录用于说明制作过程，不构成第三方权利授权证明。

### 查阅过的视觉参考

确定像素风角色的视觉方向时，查阅了以下作品。列出作者与原始页面，是为了说明参考来源；这些作品没有作为项目素材收录。

| 作者 | 作品与原始链接 | 参考范围 |
|---|---|---|
| Ignacio Polo Garzón | [Space marine](https://ignaciopolo.artstation.com/projects/V2LN2n) | 星际战士像素造型、装甲表现与角色编辑器的视觉方向 |
| DaTico | [In the grim darkness of the far future, there is only pixel art](https://www.newgrounds.com/art/view/datico/in-the-grim-darkness-of-the-far-future-there-is-only-pixel-art) | 战锤题材像素二创的表现方向 |

上述作品的权利仍归各自权利人所有。列出参考和致谢不等于获得转载、改编或商用授权，也不表示这些画师参与、认可或赞助本项目。本项目未取得上述画师的额外授权；如需使用他们的原作，应另行遵守原作许可或取得许可。

### 战锤题材与项目关系

本项目是非官方的战锤 40,000 题材桌宠，与 Games Workshop 无隶属、合作或授权关系。Warhammer 40,000、Space Marines、战团名称及相关设定、标识的权利属于其各自权利人，不能因角色图由 AI 生成就将这些既有设计宣称为本项目原创。

AI 生成不代表素材自动获得无条件使用许可，也不保证不存在第三方权利问题。本项目对代码的许可不代表授予战锤相关知识产权或参考画师作品的使用权。

### 宣传图片来源

README 的主图与三张演示图由项目维护者另行提供，当前尚未填入；不能将它们自动归入上述 AI 角色图集的来源说明。发布时请逐项补齐实际来源；如使用他人作品，请同时注明作者、原作链接和适用许可或授权情况。

| 图片 | 作者／制作方式 | 原作或来源链接 | 许可／授权情况 |
|---|---|---|---|
| 主图（`MAIN_IMAGE_URL`） | 待补充 | 待补充 | 待补充 |
| 演示图 1（`DEMO_IMAGE_1_URL`） | 待补充 | 待补充 | 待补充 |
| 演示图 2（`DEMO_IMAGE_2_URL`） | 待补充 | 待补充 | 待补充 |
| 演示图 3（`DEMO_IMAGE_3_URL`） | 待补充 | 待补充 | 待补充 |

## 文件保护

- 只接受当前用户桌面与公共桌面**直接包含的普通文件**，支持 Windows 已重定向的桌面路径。
- 不支持目录、子目录里的文件、网络路径、系统文件、重解析点、多选或永久删除。普通 `.lnk` 快捷方式只删除快捷方式本身。
- 在任务开始时记录文件长度、时间、卷号和文件 ID；命中时及 Shell 删除回调里再次核验。目标消失或发生变化时停止操作。
- 使用 `IFileOperation`，设置 `FOFX_RECYCLEONDELETE`，删除前回调拒绝没有回收标记的操作；不提供永久删除回退。
- 禁用 HTML 等文件的关联元素删除，避免连带处理同名资源文件夹。
- 回收操作失败时不会宣称成功，指挥面板和日志显示失败信息。
- 演练与 `--preview` 模式不会删除真实文件。

## 菜单与设置

仅写当前用户键：`HKCU\Software\Classes\*\shell\ForTheEmperor.Execute`。`AppliesTo` 限制桌面目录；实际执行端还会独立检查文件路径。

启动注册，关闭集成或退出时删除。一个低开销看护进程等待主进程结束，用注册所有者令牌清理异常退出遗留的菜单，避免误清理新实例。看护进程也被强制结束、断电等情况可能留下菜单；可以重新打开后退出，或执行：

```powershell
.\dist\ForTheEmperor.exe --unregister
```

设置和限长日志保存在 `%LOCALAPPDATA%\ForTheEmperor\`；日志记录已执行任务的文件名。

## 已知边界

- 本版为 Windows 传统右键菜单集成。Windows 11 一级新菜单需要 `IExplorerCommand` 和应用身份，尚未实现 MSIX/稀疏包发布。
- 优先通过 UI Automation 找到桌面图标；隐藏扩展名导致重名、图标不可见、Explorer 不提供位置或超时，会回退到右键命令点击位置附近。回退只影响动作位置，文件路径仍单独核验。
- 靠近屏幕边缘时，战士会留在可见区域，特效仍在目标位置。
- 已实现按屏幕物理坐标移动和 PerMonitorV2 DPI 声明；多显示器混合缩放需在具体设备上进一步验收。
- 原型不包含签名安装器、正式角色素材、真人配音或 Windows 11 一级菜单扩展。

## 开发与验证

```powershell
.\build.ps1 -Test
.\dist\ForTheEmperor.exe --preview
.\dist\ForTheEmperor.exe --smoke-test
.\dist\ForTheEmperor.exe --render-preview artifacts
.\dist\ForTheEmperor.exe --render-animation artifacts
```

构建使用 .NET SDK 8 或更新版本，无第三方 NuGet 包；仓库的 `NuGet.Config` 关闭远程包源。如果本机没有目标框架对应的 Windows Desktop targeting pack，需要先安装 .NET 8 SDK/targeting pack。

`--self-test artifacts` 验证状态顺序、各阶段取消、重复请求、目标变化、回收标志、真实回收、中文命名管道通信和全部战团绘制。真实回收测试仅处理它在 `artifacts\disposable-随机值\` 内创建的临时文件，并会在回收站留下带 `ForTheEmperor-recycle-test-` 前缀的测试文件。

`--smoke-test` 在预览模式中运行一次桌宠演练，15 秒后自动退出，结果写入 `artifacts\smoke-test.txt`。

`verify-integration.ps1` 使用发布后的单文件 EXE，在桌面创建一个随机命名的测试文本文件，验证菜单注册、命令进程、IPC、攻击后回收、Explorer 中图标确实消失，以及主进程异常结束后的菜单清理。通过 UI Automation 分别检查执行前图标存在、执行后图标消失，不使用 F5 或人工刷新。运行前请退出桌宠；结果写入 `artifacts\integration-results.txt`。它不使用任何已有的桌面文件。

`--render-animation` 输出八战团同屏动画 `artifacts\combat-preview.gif` 和命中对照图 `artifacts\combat-preview.png`。

主要代码：

- `PetWindow.cs`：透明桌宠窗口、拖动、移动、演出和命中调度。
- `MarineView.cs`：精灵播放、姿势切换、后坐力、跳跃与残影。
- `SpriteAtlas.cs`：透明精灵解析与战团独立攻击参数。
- `EffectView.cs`：弹道、喷火、刀光、冲击、烟尘和文件图标碎片。
- `Core.cs`：战团、状态机、配置和文件身份校验。
- `Native.cs`：屏幕坐标、桌面图标定位、回收站 COM 接口。
- `ShellIntegration.cs`：菜单注册、异常退出清理、当前用户命名管道。
- `CommandWindow.xaml`：指挥面板。

接口依据：[Windows Shell 传统菜单](https://learn.microsoft.com/en-us/windows/win32/shell/context)、[Windows 11 一级菜单集成](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer)、[IFileOperation 标志](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags)、[删除前回调](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperationprogresssink-predeleteitem)。
