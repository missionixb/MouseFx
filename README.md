# 萤火鼠 (MouseFx)

[English](README.en.md) | 简体中文

Windows 桌面鼠标特效工具：给光标加上常驻光晕、点击涟漪与粒子火花，让鼠标在任何场景下一眼可寻。程序常驻系统托盘，开机自启，几乎无感运行。

<p align="center">
  <img src="docs/images/fireworks.gif" alt="烟花：点击爆发一圈星芒" width="400"/>
  <img src="docs/images/halo-heart.png" alt="光圈：爱心形状的点击涟漪" width="360"/>
</p>
<p align="center"><sub>左：烟花点击爆发 ｜ 右：光圈爱心涟漪（涟漪形状可切换 圆圈/爱心/星星）</sub></p>

## 特效模式

同一时刻启用一种，参数独立记忆：

| 模式 | 效果 | 可调参数 |
|---|---|---|
| **光圈** | 柔和光晕跟随光标，点击时荡开涟漪 | 颜色、光晕大小/不透明度、跟随速度、涟漪半径、涟漪形状（圆圈/爱心/星星） |
| **火屑** | 火星沿轨迹迸落，重力抛物线 + 随机闪烁，寿命终点炸裂成子火星 | 颜色、粒子上限、寿命 |
| **烟花** | 星芒自光标四周绽放，像手持烟花：球面纵深、白热火星头、末端分叉 | 粒子上限、星芒直径 |

每个模式都有独立的**点击开关**（爆裂/涟漪）：关闭后点击对该特效零影响，互相独立并持久化。

## 功能

- **托盘常驻**：左/右键弹出同一 Fluent 菜单，设置与退出
- **全局设置**：静止时淡出、全屏应用自动隐藏、开机自启动、渲染帧率上限（30~144Hz）
- **界面语言**：中文 / English，切换即时生效
- **主题跟随**：设置窗口与托盘菜单跟随系统亮暗主题（WPF-UI Fluent）

## 技术要点

- 全屏透明、鼠标穿透的覆盖窗口渲染特效；Win32 低级鼠标钩子（`WH_MOUSE_LL`）只观察事件，合并调度后转发 UI 线程
- 粒子引擎：struct 对象池 + 画笔/画刷缓存 Freeze，逐帧零堆分配；画面静止时脏标记跳过重绘，空闲 CPU 占用趋近于零
- 物理帧率无关（指数衰减/半隐式欧拉），限帧只省渲染、不影响运动速度
- 健壮性：管理员窗口前台导致输入断流时优雅淡出；丢失硬件加速时自动切软件渲染并降低粒子密度；设置文件原子写入
- 119 个 xUnit 单元测试（特效物理、设置持久化、主题/语言字典完备性）

设计文档见 [docs/design.md](docs/design.md)。

## 下载安装

从 [Releases](../../releases) 页面下载 exe 直接运行（绿色单文件，无安装过程）：

| 文件 | 适用场景 |
|---|---|
| `MouseFx-vX.Y.Z-win-x64.exe` | **推荐**：自包含，无需安装任何运行时 |
| `MouseFx-vX.Y.Z-win-x64-lite.exe` | 轻量版（约 10MB）：需先安装 [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0) |

程序启动后常驻系统托盘，可在设置中开启开机自启动。

## 已知问题

**特效偶尔"发灰"，尤其在切换应用之后**——这是 Windows 11 显示链路的系统级问题，不是本应用的渲染 bug。快速自证：发灰时按 `PrtScr` 截图，截图里的特效颜色正常、只有屏幕上看着灰，即可确认是显示环节（截图抓的是合成缓冲的正确像素，灰化发生在最终扫描输出）。

原因：Win11 的 DWM 会把透明置顶窗口放进显卡的硬件叠加平面（MPO，Multi-Plane Overlay）直接输出，该路径的色彩转换与标准合成不同，半透明渐变内容会偏灰；切换应用会触发叠加平面重新分配，所以时有时无。

按顺序尝试：

1. **关闭 HDR**：设置 → 系统 → 显示 → HDR 关闭（若已开启）
2. **禁用 MPO**（管理员终端执行后重启）：

   ```powershell
   reg add "HKLM\SOFTWARE\Microsoft\Windows\Dwm" /v OverlayTestMode /t REG_DWORD /d 5 /f
   ```

   想恢复时删除该值再重启：

   ```powershell
   reg delete "HKLM\SOFTWARE\Microsoft\Windows\Dwm" /v OverlayTestMode /f
   ```

3. **更新显卡驱动**：NVIDIA / AMD / Intel 都在持续修复 MPO 相关的颜色异常

## 环境要求与构建

- Windows 10 及以上
- [.NET 10 SDK](https://dotnet.microsoft.com/download)（`net10.0-windows`）

```bash
git clone https://github.com/missionixb/MouseFx.git
cd MouseFx
dotnet build
dotnet run --project MouseFx        # 运行（常驻托盘）
dotnet test                          # 跑测试
```

## 许可证

[MIT](LICENSE)
