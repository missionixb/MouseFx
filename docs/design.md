# 鼠标特效工具（MouseFx）设计文档

日期：2026-08-27
状态：已获用户批准（2026-08-27）

## 1. 项目概述

一个 Windows 桌面鼠标特效工具：为鼠标点击添加视觉效果（波纹、粒子），并在鼠标周围显示常驻光晕，解决"找不到鼠标"的问题。程序常驻后台，通过系统托盘菜单控制各特效的开关与退出。

- 目标平台：Windows 11
- 技术栈：C# + WPF（.NET 10）
- 无第三方 NuGet 依赖

## 2. 功能范围

### 2.1 首批特效（本次实现）

1. **点击波纹（RippleEffect）**：鼠标按下时，以点击处为中心扩散一圈渐变圆环，扩散中淡出。
2. **鼠标常驻光晕（GlowEffect）**：鼠标周围常驻一圈柔光，跟随鼠标平滑移动（带缓冲，不生硬）。

### 2.2 二期特效（本次只留扩展点，不实现）

- 点击粒子迸发（ParticleEffect）：点击处迸发彩色粒子向外飞散淡出。
- 移动时光环（OrbitEffect）：鼠标移动时出现彩色旋转光环。

### 2.3 控制方式

- 系统托盘图标 + 右键菜单：每个特效一个勾选开关（RippleEffect / GlowEffect）、"开机自启动"勾选项（**默认开启**）、"退出"。
- 不实现：设置窗口、颜色/大小调节（YAGNI，二期需要再加）。

### 2.4 平台支持与跨平台策略

- 目标平台：Windows 10 / 11（.NET 10 官方支持矩阵）。不支持 Win7（微软已停止支持，不值得为此降 .NET 版本）。
- 跨平台策略（架构预留，不阻塞当前开发）：
  - 平台相关能力抽象为接口：`IMouseHookService`（鼠标事件源）、`IAutoStartService`（开机自启动）。Windows 用 WPF/Win32 实现，未来 Mac/Linux 按同一接口另写实现。
  - 托盘与特效层（OverlayWindow）在 Windows 上直接用 WPF 实现，不提前抽象（YAGNI）；跨平台时这两块按平台重写。
  - 特效核心（`IEffect` 体系）与渲染解耦：特效只维护状态数据（如 `ActiveRipples`、`Position`），`Draw` 仅把状态画到 `DrawingContext`。未来换渲染后端时特效逻辑零改动，只重写 `Draw` 适配层。现状已满足此结构，不额外引入抽象层。

## 3. 架构

```
┌─────────────┐   鼠标事件   ┌──────────────────┐
│  MouseHook  │ ──────────► │  EffectManager   │
│  (IMouseHookService)      │ 管理特效开关/转发  │
└─────────────┘             └────────┬─────────┘
                                     │ 调用 IEffect 接口
                    ┌────────────────┼────────────────┐
                    ▼                ▼                ▼
             RippleEffect     GlowEffect      (二期: Particle/Orbit)
                    └────────────────┼────────────────┘
                                     ▼
                           ┌──────────────────┐
                           │  OverlayWindow   │
                           │ 全屏透明置顶特效层 │
                           └──────────────────┘
┌─────────────┐
│ AutoStart   │  开机自启动（注册表 Run 键）
│ (IAutoStartService) │
└─────────────┘
```

### 3.1 组件职责

| 组件 | 职责 |
|---|---|
| `IMouseHookService` | 平台接口：鼠标事件源（Move/Down、Start/Stop）。Windows 实现 = `MouseHook`（Win32 `WH_MOUSE_LL` 全局低级钩子，只观察不拦截） |
| `IAutoStartService` | 平台接口：开机自启动开关（IsEnabled/Enable/Disable）。Windows 实现 = `AutoStartService`（注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`） |
| `OverlayWindow` | 全屏透明、置顶、鼠标穿透的特效绘制窗口，负责所有特效的渲染 |
| `EffectManager` | 持有特效实例列表与启用状态，把鼠标事件分发给启用的特效 |
| `IEffect` | 特效统一接口，新特效只需新建实现类并注册，不改其他代码 |
| `TrayIcon` | 系统托盘图标 + ContextMenuStrip，勾选控制特效开关与自启动、退出项 |

### 3.2 IEffect 接口定义

```csharp
public interface IEffect
{
    string Name { get; }                    // 显示名称
    bool Enabled { get; set; }              // 是否启用
    void OnMouseDown(Point pos);            // 鼠标按下（波纹触发点）
    void OnMouseMove(Point pos);            // 鼠标移动（光晕跟随目标）
    void Update(TimeSpan delta);            // 每帧更新逻辑（粒子/动画进度）
    void Draw(DrawingContext dc);           // 每帧绘制
}
```

- 特效生命周期：创建 → 启用/停用（可重复切换）→ 程序退出。
- `OverlayWindow` 每个 `CompositionTarget.Rendering` 帧：先对所有启用特效调 `Update(delta)`，再依次 `Draw(dc)`。

### 3.3 数据流

1. `MouseHook` 钩子回调（后台线程）捕获鼠标位置/按键 → `Dispatcher.Invoke` 抛到 UI 线程
2. `EffectManager` 把事件按类型分发给所有 `Enabled == true` 的特效
3. `OverlayWindow` 每帧调用各特效的 `Update` + `Draw` 完成渲染

## 4. 关键实现细节

### 4.1 特效窗口（OverlayWindow）

- `WindowStyle=None`，`AllowsTransparency=True`，`Background=Transparent`，`Topmost=True`，`ShowInTaskbar=False`
- 位置与尺寸 = 虚拟屏幕范围（`SystemParameters.VirtualScreenLeft/Top/Width/Height`，覆盖多显示器）
- **鼠标穿透**：`WindowInteropHelper` 取句柄后 `SetWindowLong` 加 `WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE`，鼠标点击直接穿透到桌面，特效窗口只是"看客"；窗口自身不接收鼠标事件，所有鼠标信息来自全局钩子

### 4.2 全局钩子（MouseHook）

- `SetWindowsHookEx(WH_MOUSE_LL, ...)`，回调里处理 `WM_MOUSEMOVE / WM_LBUTTONDOWN / WM_RBUTTONDOWN / WM_MBUTTONDOWN / WM_XBUTTONDOWN`（按下即触发波纹；本次不监听抬起，二期粒子迸发时再加 `WM_LBUTTONUP`）
- 回调必须尽快返回，事件转发用 `Dispatcher.BeginInvoke` 不阻塞钩子线程
- 钩子失效/异常时自动卸载并重新安装，最多重试 3 次，仍失败则托盘气泡提示并停用相关特效

### 4.3 点击波纹（RippleEffect）

- 按下时创建 1 个圆环对象：记录起点、半径从 0 增长到 ~60px（0.6s 内缓动），透明度同步从 0.9 衰减到 0，完成后移除
- 绘制：`DrawEllipse` + `RadialGradientBrush`（内透明→外渐隐），描边圆环用 `Pen` + 透明度插值
- 波纹对象池上限 30 个（防止连点创建过多动画对象拖慢帧率），超出时丢弃最早的
- 实现方式：不依赖 WPF `Storyboard`（对象多时开销大），在 `Update(delta)` 里手动推进 `elapsed` 与插值计算，`Draw` 用 `DrawingContext` 绘制

### 4.4 常驻光晕（GlowEffect）

- 目标位置 = 最近一次 `OnMouseMove` 的坐标；实际绘制位置以 0.25~0.35 的指数平滑系数每帧追赶目标（`pos += (target - pos) * lerp`），避免低帧率下跳变
- 绘制：半径 ~28px 的 `RadialGradientBrush` 柔光圆（中心半透明、边缘全透明），颜色可选浅蓝白（`#55FFFFFF` 中心 → 全透明边缘）
- 鼠标停止移动时保持当前位置，不消失；在 `Enabled` 关闭时直接不绘制

### 4.5 托盘（TrayIcon）

- WPF 项目 csproj 加 `<UseWindowsForms>true</UseWindowsForms>`，用 `System.Windows.Forms.NotifyIcon`（无第三方库）
- `ContextMenuStrip`：两个 CheckMenuItem（各自绑定特效的 `Enabled`，切换时同步 `IEffect.Enabled`）、分隔线、退出项
- 图标：运行时用代码生成一个简单圆点 Bitmap（避免外置 .ico 资源文件），或项目内嵌一个 16x16 图标；优先代码生成，减少资源文件
- 双击托盘图标 = 全部特效开关取反（快速总开关）

### 4.6 开机自启动（AutoStartService）

- 注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 写入值 `MouseFx = <exe 完整路径>`（`Environment.ProcessPath`）
- `Enable()` 创建/写入；`Disable()` 删除值（不存在则忽略）；`IsEnabled` 读值判断
- 默认开启：App 启动时菜单项初始为勾选态；仅当用户取消勾选才 `Disable()`
- 退出程序不修改自启动状态（"手动退出"≠"取消自启动"，由菜单项独立控制）
- 测试安全性：注册表键路径可注入，单元测试写入独立测试键并在 finally 中清理

### 4.7 线程模型

- 钩子回调在系统钩子线程 → `Dispatcher.BeginInvoke` 转发到 UI 线程
- 所有特效状态与绘制都在 UI 线程，无锁
- `CompositionTarget.Rendering` 帧回调在 UI 线程

## 5. 错误处理与边界

| 场景 | 处理 |
|---|---|
| 钩子安装失败（权限/系统限制） | 托盘气泡提示，程序仍常驻但不产生特效 |
| 钩子回调异常 | try/catch 包裹，卸载重装，最多 3 次 |
| 多显示器 | 窗口覆盖虚拟屏幕，坐标均为虚拟屏幕坐标，无需换算 |
| 特效对象过多 | 波纹池上限 30，粒子类特效二期同样设池上限 |
| 窗口被系统遮挡/失焦 | 特效层 `Topmost` + `WS_EX_NOACTIVATE`，不抢焦点、不被任务栏影响 |
| 鼠标在特效窗口上点击 | 窗口已穿透，点击直达桌面；特效位置数据来自钩子，不受影响 |
| 注册表读写异常（权限/被篡改） | try/catch 吞掉并保持当前自启动状态不变，不影响主功能 |

## 6. 测试与验收

### 6.1 单元测试（逻辑层）

- 波纹插值：`RippleEffect` 的 Update 推进后半径单调增长、透明度单调衰减、超时后对象被回收
- 光晕平滑：`GlowEffect` 位置追赶目标，无限接近且不振荡
- `EffectManager`：事件只分发给 `Enabled` 的特效；禁用后不再收到事件

### 6.2 手动验收（视觉）

1. 启动程序：桌面出现常驻光晕跟随鼠标，移动平滑
2. 左/右/中键点击：出现扩散波纹并淡出；连点 30+ 次不卡顿
3. 托盘菜单：取消勾选"光晕"→光晕消失；重新勾选→恢复；只勾选光晕时点击无波纹
4. 多显示器（如有）：特效跨屏连续，无断裂
5. 点击桌面图标/浏览器链接：点击穿透正常，特效不阻挡操作
6. 开机自启动：托盘菜单默认勾选"开机自启动"；注册表 `HKCU\...\Run` 中存在 MouseFx 值；取消勾选后值消失；重启系统后程序自动启动
7. 退出菜单：托盘图标消失、特效窗口关闭、进程退出（退出不改变自启动状态）

## 7. 项目结构

```
MouseFx/
├── MouseFx.sln
└── MouseFx/
    ├── MouseFx.csproj          # net10.0-windows, UseWPF + UseWindowsForms
    ├── App.xaml / App.xaml.cs  # 入口，启动托盘与特效层
    ├── Tray/
    │   └── TrayIcon.cs         # NotifyIcon + 菜单（含自启动项）
    ├── Hooks/
    │   └── MouseHook.cs        # WH_MOUSE_LL 全局钩子（IMouseHookService 实现）
    ├── Platform/
    │   ├── IMouseHookService.cs    # 平台接口：鼠标事件源
    │   ├── IAutoStartService.cs    # 平台接口：开机自启动
    │   └── AutoStartService.cs     # Windows 实现（注册表 Run 键）
    ├── Overlay/
    │   └── OverlayWindow.xaml / .cs  # 全屏透明特效层 + 渲染循环
    ├── Effects/
    │   ├── IEffect.cs
    │   ├── EffectManager.cs
    │   ├── RippleEffect.cs
    │   └── GlowEffect.cs
    └── tests/
        └── MouseFx.Tests/      # xUnit 测试项目（波纹插值/光晕平滑/管理器/自启动）
```

## 8. 二期扩展点（本次不做）

- `ParticleEffect` / `OrbitEffect`：各新建一个实现 `IEffect` 的类，注册进 `EffectManager`，托盘菜单加一个勾选项即可
- 颜色/大小/透明度设置窗口、开机自启：独立于特效框架，后续按需添加
