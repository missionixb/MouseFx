// UseWindowsForms 会隐式引入 System.Drawing / System.Windows.Forms，
// 与 WPF 的 System.Windows 类型同名冲突（Point、Color、Application 等）。
// 全局别名统一指向 WPF 类型；需要 Drawing 类型的地方（如 TrayIcon）用文件级别名覆盖。
global using Point = System.Windows.Point;
global using Color = System.Windows.Media.Color;
global using Application = System.Windows.Application;
