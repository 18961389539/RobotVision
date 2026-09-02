# Changelog

本文件记录 RobotVision 各版本显著变更。

## 规范

- **格式**:基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 的分组约定:`新增` / `修复` / `变更` / `移除` / `破坏性变更`
- **版本**:语义化版本(SemVer) `主.次.修订`;产线固件号 = Git tag(如 `v1.0.0`)
- **写法**:
  - 面向"现场/用户"描述,避免内部实现细节(写"支持 GigE 相机",不写"重构 CameraManager")
  - 破坏性变更必须标注,升级手册([docs/DEPLOYMENT.md](docs/DEPLOYMENT.md))同步更新
  - 每次发布时:把 `[Unreleased]` 内容挪到新版本小节,补发布日期
- **关联**:每条变更尽量标注关联 Issue/PR 编号(如 `#12`)

## [Unreleased]

### 新增
- 过程联锁(ProcessHealth):连续失败达阈值后禁止触发(1018),支持界面/CLEARINHIBIT 解除
- 资产完整性(AssetIntegrity):模型/标定档案与配方钉扎 SHA-256 校验(1017)
- 配方输出偏移(OutputOffsetOptions)
- 成功结果留档(ResultLog):每次触发成功/失败追加 `data/results/*.jsonl`(坐标/角度/置信度/耗时),供追溯与合格率/分布/趋势统计
- OK 产品存图开关(CaptureSuccess.Enabled,默认关):成功检测可存现场图
- 工艺助手(对话页):内置本地大模型问答,llama-server 本机 CPU 推理,图像/坐标/配方不出机器;可查配方、错误码、结果趋势与日志摘要;写操作需确认并留审计 `data/chat-audit/`
- 结果分析页:合格率/分布/趋势图表,结果日志改为 SQLite(页面查询)+ JSONL(留档)双写
- 模板匹配"永不翻转"约束开关(`Template.NoFlipConstraint`):分向限定工位(产品不反放)勾选后跳过 180° 分支搜索与翻转重试——省一半匹配计算,并杜绝近对称件误判 180° 的可能
- 标定向导支持直接新建比例标定档案
- 推理策略 Advisor 体系:检出阈值/特征 ROI/分割精修等参数的自动建议

### 修复
- 发布时 Configuration.Binder 源生成器报错(全局启用 C# 拦截器命名空间)
- 分析页 OxyPlot API 兼容性与编译警告

### 变更
- 全仓依赖锁定(packages.lock.json)
- 推理后端改为 OpenVINO 核显（`Inference:Provider` 默认 `OpenVinoGpu`，单会话；GPU 不可用时回退 OpenVINO CPU 并打警告；YoloDotNet 每进程只能一种 EP，已替换 CPU 包）
- WPF 界面重构为 Features/Shared 分层
- **运行时升级 .NET 8 → .NET 10 (LTS)**：目标框架全仓切换 net10.0；Microsoft.Extensions 与 Sqlite 依赖统一 10.0.11；CI 构建/发布链路上升至 .NET 10 SDK。功能与协议无变化，仅运行时升级（.NET 8 已于 2026-11-10 停止支持）
- **算法层拆分**：纯视觉算法（模板匹配/卡尺/SIFT/形状匹配）迁入独立类库 `RobotVision.Vision`——内部重构，行为与对外功能不变

### 破坏性变更

## [1.0.0] - 2026-08-27

首版交付。

### 新增
- 角度策略:双BLOB连线(免模型)/ 关键点连线 / 掩码最小外接矩形 / 分割+模板精修 / 双模型连线
- 相机接入:GigE(海康/通用)、Basler(pylon)、文件回放、虚拟;按 Id 串行取图
- TCP 行协议:PING / STATUS / TRIGGER(名称/序列号/位姿四段),PLC 联调
- 标定体系:内参(棋盘格)/ 九点外参 / 旋转中心 / 多项式 / 比例,分辨率一致性校验
- 失败现场留存(限流+缩图)与 Serilog 日志
- GitHub Actions:自动构建 + 全量测试 + 自包含 win-x64 发布(Release 下载)
- 冒烟测试补全:成功路径端到端 / TCP 应答格式 / 灯光链路 / 压力 / 失败图落盘 / 标定漂移
- 文档:[PLC 通信与 TRIGGER 协议](docs/PLC-TRIGGER-Protocol.md)、[错误码总表](docs/ERROR-CODES.md)、[部署升级手册](docs/DEPLOYMENT.md)

### 修复

### 变更

### 破坏性变更
