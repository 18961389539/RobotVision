# -*- coding: utf-8 -*-
"""Repair corrupted ? placeholders in WPF XAML (UTF-8 text loss).

Prefer running the C# tool (no Python required):
  dotnet run --project tools/FixXamlEncoding/FixXamlEncoding.csproj
"""import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "src" / "RobotVision.Wpf"

FIXES = [
    ("结果?/", "结果图 /"),
    ("结果图?ROI", "结果图 / ROI"),
    ("首次取?测试", "首次取图 / 测试"),
    ("未检出目?1007", "未检出目标 1007"),
    ("取?标定", "取图/标定"),
    ("结果?ROI", "结果图 / ROI"),
    ("名称（字?数字/_/-", "名称（字母/数字/_/-）"),
    ("PLC 触发?=未分配）", "PLC 触发，0=未分配）"),
    ("本框?2 的配方，不是列表?2 项?=未分配", "本框 #2 的配方，不是列表第 2 项；0=未分配"),
    ("工?Id（可手输", "工位 Id（可手输）"),
    ("TRIGGER ?TCP", "TRIGGER / TCP"),
    ("档案?SHA-256", "档案的 SHA-256"),
    ("返?1017", "返回 1017"),
    ("关键?A", "关键点 A"),
    ("关键?B", "关键点 B"),
    ("阈值分?连通域", "阈值分割连通域"),
    ("开运算?px)", "开运算(px)"),
    ('ToolTip="? 关闭', 'ToolTip="0 关闭'),
    ("最小间?px)", "最小间距(px)"),
    ("最大间?px)", "最大间距(px)"),
    ("卡?凸起", "卡尺凸起"),
    ("返?1019", "返回 1019"),
    ("截成?N", "截成前 N"),
    ("（?±3°）", "（约 ±3°）"),
    ('Text="?已示教模"', 'Text="✓ 已示教模板"'),
    ("阈?边缘?特征框", "阈值/边缘/特征框"),
    ("会?0.85", "会按 0.85"),
    ("定角?+", "定角 +"),
    ("检?ROI", "检测 ROI"),
    ("绿?检测", "绿色=检测"),
    ("吸?料厚", "吸盘/料厚"),
    ("?示教 = Δ", "− 示教 = Δ"),
    ("点一?Δ?", "点一次累加 Δ"),
    ("生?TRIGGER", "生产 TRIGGER"),
    ("检测区?ROI", "检测区域 ROI"),
    ("左上?X（", "左上角 X（"),
    ("左上?Y（", "左上角 Y（"),
    ("左上?X?~1", "左上角 X，0~1"),
    ("左上?Y?~1", "左上角 Y，0~1"),
    ("宽度?~1", "宽度，0~1"),
    ("高度?~1", "高度，0~1"),
    ("控制?Id", "控制器 Id"),
    ("保?测试触发", "保证测试触发"),
    ("（ms? = 永久", "（ms，0 = 永久）"),
    ("可点?0天」", "可设 30 天」"),
    ("backlog?~1024", "backlog，默认 ~1024"),
    ("排队?accept", "排队待 accept"),
    ("上限? = 不限", "上限，0 = 不限"),
    ("直?CPU", "直连 CPU"),
    ("上限? = 不限制）", "上限，0 = 不限制）"),
    ("校验?012/1014", "校验（1012/1014）"),
    ("数量? = 不按数量", "数量，0 = 不按数量"),
    ("保留? = 不按天", "保留，0 = 不按天"),
    ("仅产?TRIGGER", "仅产线 TRIGGER"),
    ("不入库?007", "不入库；1007"),
    ("须勾?SQLite", "须勾选 SQLite"),
    ("次数? = 不联锁）", "次数，0 = 不联锁）"),
    ("TSV? = 不按天", "TSV，0 = 不按天"),
    ("PLC ?CLEARINHIBIT", "PLC 发 CLEARINHIBIT"),
    ("错误?001/1004", "错误（1001/1004"),
    ("；?= 允许所有", "；空 = 允许所有"),
    ("通配?192.168", "通配如 192.168"),
    ("合?IP", "合法 IP"),
    ("?= 保存后生", "⚡ = 保存后生效"),
    ("折叠分?+", "折叠分组 +"),
    ("?运行 / ?未启", "● 运行 / ○ 未启动"),
    ("外包一?DynamicScrollViewer?", "外包一层 DynamicScrollViewer；"),
    ("滚轮链式传递?", "滚轮链式传递；"),
    ("不要?Disabled", "不要用 Disabled"),
    ("逐页修改?====", "逐页修改）===="),
    ("未覆?DataGrid", "未覆盖 DataGrid"),
    ("实时预?/", "实时预览 /"),
    ('字?数字/_/-', "字母/数字/_/-"),
    ("字段?EditType", "字段随 EditType"),
    ("（ms? = 不限速）", "（ms，0 = 不限速）"),
    ("已枚举?Basler", "已枚举的 Basler"),
    ("留?不下发", "留空则不下发"),
    ("设置中?TRIGGER", "设置中的 TRIGGER"),
    ("棋盘?圆点", "棋盘/圆点"),
    ("标准?σ?", "标准差 σ；0"),
    ("默?15×11", "默认 15×11"),
    ("json ?Cameras", "json 的 Cameras"),
    ('合适后?quot;填入编辑?quot;', "合适后「填入编辑区」"),
    ('再?quot;保存&quot;', "再「保存」"),
    ("推理状?结果", "推理状态 / 结果"),
    ("检?分类", "检测/分类"),
    ("经配?相机", "经过配方/相机"),
    ("所?bmp", "所有 bmp"),
    ("上一?下一张", "上一张/下一张"),
    ("迷你卡? 个", "迷你卡片（4 个"),
    ("进?VisionService", "进入 VisionService"),
    ("错误?001", "错误如 1001"),
    ("历史」出?ERR", "历史」出现 ERR"),
    (' · 最?"', ' · 最近 "'),
    ('最近触?"', '最近触发 "'),
    ("替?DataGrid", "替代 DataGrid"),
    ("最? ", "最近 "),
    ("按配?/", "按配方 /"),
    ("?WebView2", "用 WebView2"),
    ("图像主?+", "图像主区 +"),
    ("点选取?", "点选角点 ·"),
    ("眼在?/", "眼在手 /"),
    ("TCP ?RZ", "TCP 与 RZ"),
    ("拍照?TCP", "拍照点 TCP"),
    ("拍照?RZ ?°)", "拍照点 RZ (°)"),
    ("基坐标?/", "基坐标 /"),
    ("说?+", "说明 +"),
    ("首角点，?棋盘", "首角点，沿棋盘"),
    ("工件平面 ?取图", "工件平面 → 取图"),
    ("示教流?+", "示教流程 +"),
    ("?4 轴每", "第 4 轴每"),
    ("建?5~9", "建议 5~9"),
    ("点表填 ? 个", "点表填 ≥ 3 个"),
    ("零件??δ", "零件角 − δ"),
    ("点?? 个", "点 ≥ 3 个"),
    ("外?旋转中心", "外参/旋转中心"),
    ('Header="?轴角"', 'Header="第4轴角"'),
    ("启动时?data", "启动时从 data"),
    ("三类档?TabControl", "三类档案 TabControl"),
    ('最大残?"', '最大残差"'),
    ('分辨?"', '分辨率"'),
    ("浮动面?428px", "浮动面板 428px"),
    ("像素 ?毫米", "像素 → 毫米"),
    ("比?(mm/px)", "比例 (mm/px)"),
    ("配?外参", "配方外参"),
    ("物?mm", "物体 mm"),
    ('Content="?X"', 'Content="填 X"'),
    ('Content="?Y"', 'Content="填 Y"'),
    ("确?ResultLog", "确认 ResultLog"),
    ("视觉系?·", "视觉系统 ·"),
    ("）?相机", "）· 相机"),
    ("实时预?·", "实时预览 ·"),
    ("去畸?·", "去畸变 ·"),
    ("外接矩?双模型", "外接矩形 / 双模型"),
    ("中心连?关键", "中心连线 / 关键"),
    ("中心补?·", "中心补偿 ·"),
    ("?UI 与", "本 UI 与"),
    ("触发?#10;", "触发。&#10;"),
    ("避?TypographyOverrides 自引?BasedOn", "避免 TypographyOverrides 自引用 BasedOn"),
    ("?WPF-UI DefaultComboBox 上只改前?底板，保?Fluent 模板与下拉铬", "在 WPF-UI DefaultComboBox 上只改前景底板，保留 Fluent 模板与下拉样式"),
    ("结果图视\"", "结果图视图\""),
    ("位姿叠加结\"", "位姿叠加结果\""),
    ('Header="推理与角"', 'Header="推理与角度"'),
    ("匹配阈\"", "匹配阈值\""),
    ("固定阈\"", "固定阈值\""),
    ("Otsu 自动阈\"", "Otsu 自动阈值\""),
    ("框选特\"", "框选特征\""),
    ("记下本次为示教输\"", "记下本次为示教输出\""),
    ("用结果库合格均值建议补\"", "用结果库合格均值建议补偿\""),
    ('Header="检测区域（ROI"', 'Header="检测区域（ROI）"'),
    ("启用（取图前点亮\"", "启用（取图前点亮）\""),
    ('Text="控制" Margin="0,0,0,4" FontSize="11" />', 'Text="控制器" Margin="0,0,0,4" FontSize="11" />'),
    ("通道（≥1\"", "通道（≥1）\""),
    ("精修失败时回退粗角（无方向，不推荐\"", "精修失败时回退粗角（无方向，不推荐）\""),
    ("边缘图定角（更准\"", "边缘图定角（更准）\""),
    ("主模型（.onnx\"", "主模型（.onnx）\""),
    ("次模型（B 特征 .onnx\"", "次模型（B 特征 .onnx）\""),
    ("按名称或备注过滤（F5 刷新\"", "按名称或备注过滤（F5 刷新）\""),
    ('Text="" FontSize="9" Foreground="Black"', 'Text="停" FontSize="9" Foreground="Black"'),
    ("光照稳定时建议固定阈值更\"", "光照稳定时建议固定阈值更稳\""),
    ("框选区\"", "框选区域\""),
    ('Text="拍照位姿校验（OnArm"', 'Text="拍照位姿校验（OnArm）"'),
]


def fix_w_h_px(content: str) -> str:
    if "?(px)" not in content:
        return content
    parts = content.split("?(px)")
    out = [parts[0]]
    labels = ["W", "H"]
    for i, part in enumerate(parts[1:]):
        out.append(f"{labels[i % 2]}(px)")
        out.append(part)
    return "".join(out)


def fix_bytes(content: str) -> str:
    content = content.replace(
        '<Run Text=" · ?" /><Run Text="{Binding BytesIn',
        '<Run Text=" · 入" /><Run Text="{Binding BytesIn',
    )
    content = content.replace(
        '<Run Text=" · ?" /><Run Text="{Binding BytesOut',
        '<Run Text=" · 出" /><Run Text="{Binding BytesOut',
    )
    return content


def fix_empty_wh(content: str) -> str:
    content = content.replace(
        '<TextBlock Text="" Margin="0,0,0,4" FontSize="11" ToolTip="宽度，0~1 相对比例" />',
        '<TextBlock Text="W" Margin="0,0,0,4" FontSize="11" ToolTip="宽度，0~1 相对比例" />',
    )
    content = content.replace(
        '<TextBlock Text="" Margin="0,0,0,4" FontSize="11" ToolTip="高度，0~1 相对比例" />',
        '<TextBlock Text="H" Margin="0,0,0,4" FontSize="11" ToolTip="高度，0~1 相对比例" />',
    )
    return content


def main() -> None:
    changed = []
    for path in ROOT.rglob("*.xaml"):
        text = path.read_text(encoding="utf-8")
        orig = text
        for old, new in FIXES:
            text = text.replace(old, new)
        text = fix_w_h_px(text)
        text = fix_bytes(text)
        text = fix_empty_wh(text)
        if text != orig:
            path.write_text(text, encoding="utf-8", newline="\n")
            changed.append(path.relative_to(ROOT))
            print("fixed:", path.name)
    print("total:", len(changed))


if __name__ == "__main__":
    main()
