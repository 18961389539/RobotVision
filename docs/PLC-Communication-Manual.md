# RobotVision 工业视觉引导系统 — PLC 通信接口与集成手册

> **文档版本**：v2.2  
> **适用对象**：PLC 工程师、机器人调试工程师、上位机系统集成工程师  
> **协议类型**：TCP/IP ASCII 纯文本逗号分隔行协议（以 `\n` 结尾）  
> **最新修订**：2026-08  

---

## 目录

1. [通信网络规范与参数配置](#1-通信网络规范与参数配置)
2. [协议命令集总览](#2-协议命令集总览)
3. [基础触发（v1）与位姿触发（v2）设计原理](#3-基础触发v1与位姿触发v2设计原理)
4. [详细命令规范](#4-详细命令规范)
   - 4.1 [PING 心跳保活](#41-ping--心跳保活)
   - 4.2 [STATUS 状态与队列查询](#42-status--状态与队列查询)
   - 4.3 [TRIGGER 拍照与定位引导](#43-trigger--拍照与定位引导)
5. [错误代码全表与 PLC 处置状态机](#5-错误代码全表与-plc-处置状态机)
6. [核心安全机制：1012 拍照位姿一致性校验（防撞机）](#6-核心安全机制1012-拍照位姿一致性校验防撞机)
7. [标准交互时序图](#7-标准交互时序图)
8. [PLC 编程参考实现 (IEC 61131-3 ST 结构化文本)](#8-plc-编程参考实现-iec-61131-3-st-结构化文本)
9. [现场联调与排错 Checklist](#9-现场联调与排错-checklist)

---

## 1. 通信网络规范与参数配置

| 参数项 | 规范要求 | 说明 |
| :--- | :--- | :--- |
| **传输协议** | TCP/IP Socket | 视觉系统作为 **TCP Server（服务端）**，PLC 作为 **TCP Client（客户端）** |
| **默认端口** | `9999` | 可在视觉软件 `appsettings.json` 或 UI「通信设置」中配置 |
| **监听 IP** | `0.0.0.0` | 默认监听全部网卡；支持配置 IP 白名单（如 `192.168.1.*`） |
| **字符编码** | **UTF-8 / ASCII** | 禁止在通讯报文中夹带中文字符或全角标点 |
| **帧结束符** | **`\n` (LF, 0x0A)** | 每条发送与接收报文均必须以 `\n` 结尾（服务端兼容容忍 `\r\n`） |
| **数值格式** | **ASCII 十进制浮点数** | 小数点必须为英文点号 `.`，负号为 `-`。**严禁包含逗号**（逗号作为字段分隔符） |
| **并发与流控** | 单连接串行半双工交互 | 每个客户端连接上遵循“**一发一收**”原则；收到当前应答后方可发送下一条请求 |
| **超时建议** | 视觉处理超时 5000ms | PLC 端 Socket 接收超时建议配置为 **10000ms**（≥ 2 × 视觉超时）。**连接默认不因空闲断开**（`IdleTimeoutMs=0`） |

---

## 2. 协议命令集总览

系统支持 **4 种核心指令**，大小写不敏感（推荐全大写）：

| 请求报文 | 正常应答报文 | 适用场景与功能 |
| :--- | :--- | :--- |
| `PING\n` | `PONG\n` | 链路心跳检测（可选；默认不断开空闲连接） |
| `STATUS\n` | `OK,ready\|busy,队列深度,队列上限,上次耗时ms\n` | 触发前管线状态查询，避免任务堆叠 |
| `TRIGGER,配方名\n` | `OK,配方名,目标数,X1,Y1,RZ1,...,耗时ms\n` | **v1 基础触发**：固定相机 / 免位姿校验工位 |
| `TRIGGER,配方名,X,Y,RZ\n` | `OK,配方名,目标数,X1,Y1,RZ1,...,耗时ms\n` | **v2 位姿触发**：末端相机（Eye-in-Hand）防呆引导 |

> **注**：若发送未定义命令，视觉系统统一回复 `ERR,1000,UNKNOWN_COMMAND\n`。

---

## 3. 基础触发（v1）与位姿触发（v2）设计原理

视觉引导根据相机物理安装方式及安全需求划分为两种触发模式：

```
                    ┌── 1. 固定相机 (Eye-to-Hand) ──► 基础触发 (v1): TRIGGER,A01
                    │
工业相机安装工况 ────┤
                    │
                    └── 2. 末端相机 (Eye-in-Hand) ──► 位姿触发 (v2): TRIGGER,A01,X,Y,RZ
                                                      (带 1012 位姿防呆拦截)
```

### 3.1 差异对比表

| 对比维度 | 基础触发（v1 格式） | 位姿触发（v2 格式） |
| :--- | :--- | :--- |
| **报文格式** | `TRIGGER,配方名\n` | `TRIGGER,配方名,X,Y,RZ\n` |
| **适用工况** | **固定相机（Eye-to-Hand）**<br>相机固定在机架/治具上方，不随机器人运动。 | **末端相机（Eye-in-Hand / On-Arm）**<br>相机安装在机械臂末端，随机器人一起运动。 |
| **数学标定机理** | 标定矩阵与机器人当前位置无关，永久有效。 | 标定矩阵**仅在标定基准位姿下成立**。拍照点一旦偏移，计算坐标全错。 |
| **安全防呆机制** | 固定机架无校验。末端相机若仍发 v1，返回 **1014** | 自动比对当前位姿与标定档案，**超差立即返回 1012** |
| **PLC 编程复杂度** | 极简，无需读取机器人实时位姿寄存器。 | 需要在机器人完全停止后读取 $(X, Y, RZ)$ 拼入报文。 |

### 3.2 选型建议
* **固定在机架上的相机**：使用 `TRIGGER,配方名\n` 即可；
* **安装在机械臂末端的相机**：**必须**使用 `TRIGGER,配方名,X,Y,RZ\n`；
* **产线程序统一化**：PLC 可以统一封装 `TRIGGER,配方名,X,Y,RZ\n` 功能块。固定相机工位即使上报了位姿，视觉服务端也会识别并自动忽略，无任何负面影响。

---

## 4. 详细命令规范

### 4.1 PING — 心跳保活

用于周期性检测 Socket 通信链路是否畅通及视觉程序是否处于运行状态。

* **PLC 发送**：
  ```text
  PING\n
  ```
* **视觉应答**：
  ```text
  PONG\n
  ```
* **PLC 处置逻辑**：默认连接不因空闲断开，无需为保活而周期 PING。若要用 PING 检测视觉进程存活，间隔 5～30 秒即可。连续多次无 `PONG` 时判定断开并重连。

---

### 4.2 STATUS — 状态与队列查询

在发送高频拍照指令前，预判视觉系统当前是否空闲、后台排队状况。

* **PLC 发送**：
  ```text
  STATUS\n
  ```
* **视觉应答**：
  ```text
  OK,状态,当前队列深度,最大队列上限,上次处理耗时ms\n
  ```

#### 字段详细说明：
| 字段序号 | 字段名称 | 数据类型 | 示例值 | 说明 |
| :---: | :--- | :---: | :---: | :--- |
| 1 | 标志位 | STRING | `OK` | 固定为 OK |
| 2 | 系统状态 | STRING | `ready` / `busy` | `ready`: 未执行且队列为空；`busy`: 正在处理或队列非空 |
| 3 | 当前队列深度 | INT | `0` | 当前正在排队等待处理的任务个数 |
| 4 | 最大队列上限 | INT | `4` | 视觉管线最大允许排队深度（超限触发 1009） |
| 5 | 上次处理耗时 | INT | `125` | 上一次视觉推理执行耗时（单位：ms） |

#### 报文示例：
```text
请求: STATUS\n
应答: OK,ready,0,4,128\n   ← 系统空闲，队列 0/4，上次耗时 128ms
应答: OK,busy,2,4,850\n    ← 系统正忙，已有 2 个任务在排队
```

---

### 4.3 TRIGGER — 拍照与定位引导

通知视觉系统执行全流程引导：**点亮指定通道光源 → 相机曝光采图 → 畸变校正/多项式映射 → AI 识别定位 → 机器人基坐标系转换 → 输出目标位姿**。

#### 4.3.1 请求格式

##### ① v1 格式（基础单段，适用于固定机架相机）
```text
TRIGGER,配方名\n
```
* 示例：`TRIGGER,A01\n`

##### ② v2 格式（四段式位姿，适用于末端随动相机）
```text
TRIGGER,配方名,X,Y,RZ\n
```
* 示例：`TRIGGER,A01,152.340,-210.500,45.000\n`

#### 字段定义：
| 字段 | 必填 | 单位 | 说明 |
| :--- | :---: | :---: | :--- |
| `配方名` | 是 | - | 视觉软件中配置的配方 ID（允许字母/数字/下划线/中划线，**禁止路径符号**） |
| `X` | v2 | mm | **拍照瞬间**机器人法兰中心（TCP）在机器人基坐标系下的 X 坐标（3位小数） |
| `Y` | v2 | mm | **拍照瞬间**机器人 TCP 在机器人基坐标系下的 Y 坐标（3位小数） |
| `RZ` | v2 | deg | **拍照瞬间**机器人第 4 轴（末端旋转轴）当前角度（范围 $\pm 180^\circ$ 或 $0 \sim 360^\circ$） |

> ⚠️ **PLC 采集位姿重要原则**：
> 1. 必须在机器人到位信号（In-Position）置位且**完全停止**后读取当前位姿寄存器；
> 2. `X`、`Y`、`RZ` 三个数值必须取自**同一时刻**的位置采样。

---

#### 4.3.2 成功应答（OK）

```text
OK,配方名,目标数量,X1,Y1,RZ1,X2,Y2,RZ2,...,总耗时ms\n
```

#### 字段拆解结构：
$$\underbrace{\text{OK}}_{1},\underbrace{\text{A01}}_{2},\underbrace{N}_{3},\underbrace{X_1,Y_1,RZ_1}_{\text{第1个目标}},\dots,\underbrace{X_N,Y_N,RZ_N}_{\text{第N个目标}},\underbrace{\text{ElapsedMs}}_{\text{末尾耗时}}\text{\textbackslash n}$$

| 序号 | 字段名 | 类型 | 单位 | 示例值 | 说明 |
| :---: | :--- | :---: | :---: | :---: | :--- |
| **1** | 成功响应头 | STRING | - | `OK` | 识别为成功报文 |
| **2** | 配方名 | STRING | - | `A01` | 回显触发的配方 ID |
| **3** | **目标数量 $N$** | INT | - | `2` | **检出的目标个数。后续为 $3 \times N$ 个坐标字段** |
| **4** | 目标1 X | REAL | mm | `102.356` | 目标在机器人基坐标系下的 X 坐标 |
| **5** | 目标1 Y | REAL | mm | `-88.412` | 目标在机器人基坐标系下的 Y 坐标 |
| **6** | 目标1 RZ | REAL | deg | `45.120` | 目标在机器人基坐标系下的引导角度（$(-180, 180]$） |
| **...**| ... | ... | ... | ... | 若 $N>1$，依次追加目标 2、目标 3 的 X,Y,RZ |
| **末尾** | 处理耗时 | INT | ms | `342` | 从收到触发到计算完成输出的总耗时 |

#### 报文示例：
* **单目标检出 ($N=1$)**：
  ```text
  OK,A01,1,250.410,-120.330,90.150,215\n
  ```
* **双目标检出 ($N=2$)**：
  ```text
  OK,A01,2,100.120,50.200,-15.300,180.400,60.100,164.700,380\n
  ```

---

#### 4.3.3 失败应答（ERR）

```text
ERR,错误码,错误描述信息\n
```

* **示例**：
  ```text
  ERR,1007,未检出目标\n
  ERR,1012,拍照位姿不一致(当前与标定偏差超容差)\n
  ERR,1013,INVALID_POSE_NUMBER\n
  ```

---

## 5. 错误代码全表与 PLC 处置状态机

| 错误码 | 枚举名称 | 产生原因 | 是否可重试 | PLC 处置策略与动作 |
| :---: | :--- | :--- | :---: | :--- |
| **1000** | `UnknownCommand` | 指令拼写错误（非 PING/STATUS/TRIGGER） | 否 | **停机报警**。检查 PLC 发送字符串组包逻辑。 |
| **1001** | `UnknownRecipe` | 配方不存在、被禁用或配方名包含非法字符 | 否 | **停机报警**。核对触摸屏配方号与视觉内命名。 |
| **1002** | `CameraNotRegistered`| 配方绑定的相机 ID 未在视觉系统中注册 | 否 | **报警通知视觉工程师**。检查视觉相机配置。 |
| **1003** | `CameraGrabFailed` | 相机采图超时、掉线或硬件采图失败 | **可重试 1 次** | 延时 500ms 重试 1 次；若仍失败报“相机故障”。 |
| **1004** | `NotCalibrated` | 工位未做外参/多项式标定，或分辨率不匹配 | 否 | **停机报警**。提示视觉工程师完成标定。 |
| **1005** | `ModelNotAvailable`| AI 深度学习模型文件缺失或加载失败 | 否 | **停机报警**。检查视觉模型文件。 |
| **1006** | `LightNotRegistered`| 配方引用的光源控制器未注册或初始化失败 | 否 | **停机报警**。检查串口/网口光源控制器连接。 |
| **1007** | `NoTargetFound` | 画面内未识别到符合置信度阈值的工件 | **可重试 1~2 次** | 延时 200ms 重试；连续未检出转抛料/人工干预。 |
| **1008** | `Timeout` | 视觉处理总耗时超过配置上限（如 >5000ms）| **先查后试** | **不可立即重发**。发 `STATUS` 确认 `ready` 后再重试。 |
| **1009** | `Busy` | 视觉并发排队超限（前序任务尚未结束） | **可重试** | 等待 300ms 后重试（建议结合 STATUS 等待）。 |
| **1010** | `QueueTimeout` | 请求在排队等待空闲槽位阶段超时（未执行） | **可立即重试** | 任务未进入推理，可直接重发。 |
| **1011** | `CameraInitFailed` | 相机 SDK 初始化失败（网卡/驱动/线缆异常） | 否 | **停机报警**。检查相机供电、网线与驱动。 |
| **1012** | `PoseMismatch` | **末端相机实际拍照位姿与标定基准位姿超差** | **排查后重试** | **严禁直接屏蔽**。核对机器人拍照点示教点位（详见第6章）。 |
| **1013** | `InvalidTriggerArgument`| TRIGGER 参数格式错误（段数不对或数值非法）| 否 | **停机报警**。检查 PLC 字符串拼接与浮点格式。 |
| **1014** | `PoseRequired` | 末端相机工位未上报 X,Y,RZ | 否 | **改为四段式 TRIGGER**。界面手动触发须勾选「上报拍照位姿」。 |
| **1099** | `InternalError` | 视觉内部未捕获异常 | 否 | **报警并记录时间点**，供视觉工程师排查日志。 |

---

## 6. 核心安全机制：1012 拍照位姿一致性校验（防撞机）

### 6.1 为什么必须做位姿防呆校验？
当相机安装在机械臂末端（Eye-in-Hand / On-Arm）时，**标定矩阵仅在当时的拍照基准位姿下数学成立**。  
若机械臂因示教点被误修改、切换机型后未调到对应点位、或者定位未停稳产生漂移：
* **没有 1012 校验时**：视觉会算出完全错位的机器人坐标，导致机械手撞机或扎坏工件；
* **启用 1012 校验时**：视觉系统在取图前比对 PLC 传入的 $(X, Y, RZ)$ 与标定档案的基准位姿，一旦超出容差立即返回 `ERR,1012` 拦截，杜绝事故。

### 6.2 容差判定规则
* **平移误差**：
  $$\Delta XY = \sqrt{(X_{PLC} - X_{标定})^2 + (Y_{PLC} - Y_{标定})^2} \le 0.50\text{ mm}$$
* **旋转误差**：
  $$\Delta RZ = |\text{Normalize}(RZ_{PLC} - RZ_{标定})| \le 0.50^\circ \quad (\text{跨越 } \pm180^\circ \text{ 边界自动平滑转换})$$

### 6.3 收到 ERR,1012 时的 PLC 排查流程图

```
                收到 ERR,1012
                      │
           PLC 检查采样时机与寄存器
                      │
        ┌─────────────┴─────────────┐
   [采样时机过早]              [点位已完全停稳]
   (未停稳即读数)              (确实与标定点有偏差)
        │                           │
  增加 50ms 延时               核对示教程序
  或等待 In-Pos 信号                │
        │                  ┌────────┴────────┐
     重新触发         [示教点被误动]     [工艺要求更改拍照点]
                           │                 │
                      恢复原示教点位     联系视觉工程师
                                       重新标定该工位外参
```

---

## 7. 标准交互时序图

```
   PLC 控制器                                视觉工控机 (RobotVision)
       │                                              │
       ├────────────── 建立 TCP 连接 (Port:9999) ────►│ (白名单校验)
       │                                              │
  [周期心跳]                                          │
       ├────────────── PING\n ───────────────────────►│
       │◄───────────── PONG\n ────────────────────────┤
       │                                              │
  [准备生产]                                          │
       │── 机器人移动到拍照点 ──┐                      │
       │◄─ 到位信号置位(InPos) ─┘                      │
       │                                              │
       ├────────────── STATUS\n ─────────────────────►│ (可选防御轮询)
       │◄───────────── OK,ready,0,4,120\n ────────────┤
       │                                              │
  [触发采图]                                          │
       ├────────────── TRIGGER,A01,100.0,200.0,0.0\n ─►│ 1. 校验拍照位姿 (1012?)
       │                                              │ 2. 触发点亮光源
       │                                              │ 3. 相机曝光取图 (1003?)
       │                                              │ 4. 熄灭光源
       │                                              │ 5. AI 模型推理 (1007?)
       │                                              │ 6. 转换机器人基坐标
       │◄───────────── OK,A01,1,102.3,-50.1,89.5,210\n─┤ 
       │                                              │
  [执行动作]                                          │
       │── 校验数据合法性 (非NaN/非零)                 │
       │── 机器人前往目标坐标执行抓取/贴装             │
```

---

## 8. PLC 编程参考实现 (IEC 61131-3 ST 结构化文本)

以下提供通用 PLC（汇川、西门子 S7-1200/1500、倍福 TwinCAT、欧姆龙等）的 ST 语言状态机参考实现。

```iecst
TYPE E_VisionState :
(
    STATE_IDLE          := 0,  // 空闲状态
    STATE_CHECK_READY   := 1,  // 状态预检
    STATE_SEND_TRIGGER  := 2,  // 发送触发
    STATE_WAIT_REPLY    := 3,  // 等待并接收应答
    STATE_PARSE_DATA    := 4,  // 解析坐标
    STATE_ERROR_HANDLER := 99  // 错误与重试处置
);
END_TYPE

FUNCTION_BLOCK FB_RobotVisionClient
VAR_INPUT
    bExecute        : BOOL;        // 启动触发信号（上升沿）
    sRecipeName     : STRING[32];  // 配方名 (如 'A01')
    fCurrentTcpX    : LREAL;       // 机器人当前 X (mm)
    fCurrentTcpY    : LREAL;       // 机器人当前 Y (mm)
    fCurrentTcpRZ   : LREAL;       // 机器人当前 RZ (deg)
    tTimeout        : TIME := T#8S;// 超时时间
END_VAR
VAR_OUTPUT
    bDone           : BOOL;        // 引导完成
    bBusy           : BOOL;        // 正在运行
    bError          : BOOL;        // 发生错误
    nErrorCode      : INT;         // 错误代码 (0=无错误)
    nTargetCount    : INT;         // 检出目标数
    fTargetX        : LREAL;       // 目标 1 X 坐标
    fTargetY        : LREAL;       // 目标 1 Y 坐标
    fTargetRZ       : LREAL;       // 目标 1 RZ 角度
END_VAR
VAR
    nState          : E_VisionState := STATE_IDLE;
    tonTimer        : TON;
    sSendBuffer     : STRING[255];
    sRecvBuffer     : STRING[512];
    nRetryCount     : INT := 0;
END_VAR

// ---------------- 逻辑处理 ----------------
CASE nState OF
    STATE_IDLE:
        bDone  := FALSE;
        bError := FALSE;
        nErrorCode := 0;
        IF bExecute THEN
            bBusy := TRUE;
            nRetryCount := 0;
            nState := STATE_SEND_TRIGGER;
        ELSE
            bBusy := FALSE;
        END_IF

    STATE_SEND_TRIGGER:
        // 1. 组装 TRIGGER 报文（强制点号小数，行尾追加 LF: $0A）
        // 格式: TRIGGER,A01,100.230,200.500,45.000\n
        sSendBuffer := CONCAT('TRIGGER,', sRecipeName);
        sSendBuffer := CONCAT(sSendBuffer, ',');
        sSendBuffer := CONCAT(sSendBuffer, LREAL_TO_STRING_FORMAT(fCurrentTcpX, '%.3f'));
        sSendBuffer := CONCAT(sSendBuffer, ',');
        sSendBuffer := CONCAT(sSendBuffer, LREAL_TO_STRING_FORMAT(fCurrentTcpY, '%.3f'));
        sSendBuffer := CONCAT(sSendBuffer, ',');
        sSendBuffer := CONCAT(sSendBuffer, LREAL_TO_STRING_FORMAT(fCurrentTcpRZ, '%.3f'));
        sSendBuffer := CONCAT(sSendBuffer, '$0A'); // $0A 即 \n

        // 2. 调用底层 Socket 发送函数
        TcpSend(sSendBuffer);
        tonTimer(IN := FALSE);
        nState := STATE_WAIT_REPLY;

    STATE_WAIT_REPLY:
        // 接收超时监控
        tonTimer(IN := TRUE, PT := tTimeout);
        
        IF TcpReceive(sRecvBuffer) THEN // 收到以 \n 结尾的完整数据帧
            tonTimer(IN := FALSE);
            nState := STATE_PARSE_DATA;
        ELSIF tonTimer.Q THEN
            tonTimer(IN := FALSE);
            nErrorCode := 1008; // 本地标记为超时
            nState := STATE_ERROR_HANDLER;
        END_IF

    STATE_PARSE_DATA:
        // 解析返回字符串
        IF FIND(sRecvBuffer, 'OK,') = 1 THEN
            // 拆分字段: OK,A01,1,102.356,-88.412,45.120,342
            nTargetCount := STRING_TO_INT(SplitField(sRecvBuffer, 3)); // 目标数
            IF nTargetCount >= 1 THEN
                fTargetX  := STRING_TO_LREAL(SplitField(sRecvBuffer, 4));
                fTargetY  := STRING_TO_LREAL(SplitField(sRecvBuffer, 5));
                fTargetRZ := STRING_TO_LREAL(SplitField(sRecvBuffer, 6));
                bDone := TRUE;
                bBusy := FALSE;
                nState := STATE_IDLE;
            ELSE
                nErrorCode := 1007; // 目标数为 0
                nState := STATE_ERROR_HANDLER;
            END_IF
        ELSIF FIND(sRecvBuffer, 'ERR,') = 1 THEN
            // 拆分错误码: ERR,1012,MESSAGE
            nErrorCode := STRING_TO_INT(SplitField(sRecvBuffer, 2));
            nState := STATE_ERROR_HANDLER;
        ELSE
            nErrorCode := 1000;
            nState := STATE_ERROR_HANDLER;
        END_IF

    STATE_ERROR_HANDLER:
        // 根据错误码决定处置策略
        CASE nErrorCode OF
            1003, 1007, 1009, 1010: // 可重试类故障
                IF nRetryCount < 2 THEN
                    nRetryCount := nRetryCount + 1;
                    nState := STATE_SEND_TRIGGER;
                ELSE
                    bError := TRUE;
                    bBusy := FALSE;
                    nState := STATE_IDLE;
                END_IF

            1008: // 超时类故障
                bError := TRUE;
                bBusy := FALSE;
                nState := STATE_IDLE;

            1012: // 拍照位姿不一致故障，坚决停机报警
                bError := TRUE;
                bBusy := FALSE;
                nState := STATE_IDLE;

            ELSE // 1001, 1002, 1004, 1005, 1006, 1011 等配置与硬件故障
                bError := TRUE;
                bBusy := FALSE;
                nState := STATE_IDLE;
        END_CASE
END_CASE
```

---

## 9. 现场联调与排错 Checklist

在车间现场调试或与视觉系统联调时，请依次对照以下项目排查：

1. **网络连通性测试**：
   * 在 PLC 工程师电脑上使用调试工具（如 NetAssist / SocketTool）连接视觉 IP 和端口 `9999`。
   * 发送 `PING\n`，应立即收到 `PONG\n`。
2. **报文结束符检查**：
   * 确认发送的所有字符串尾部都包含了 `0x0A`（换行符 `\n`）。若无换行符，视觉服务端的接收流会一直等待直到超时。
3. **小数与符号格式**：
   * 确认浮点数转换为字符串时，小数点为英文点 `.`。严禁输出类似 `100,50`（欧洲部分 PLC 本地化格式）的报文。
4. **角度坐标系对应**：
   * 视觉软件输出的角度范围固定为 **$(-180.000^\circ, 180.000^\circ]$**。
   * 若机器人控制器（如某些 SCARA 品牌）的角度系统为 $[0^\circ, 360^\circ)$，PLC 端应做简单转换：`IF Angle < 0 THEN Angle := Angle + 360.0; END_IF;`。
5. **OnArm 末端工位防呆测试（必测项）**：
   * 将机器人移动到拍照点后，手动在报文中将 X 坐标篡改偏移 $+2.0\text{mm}$ 发送 `TRIGGER,配方名,X+2,Y,RZ\n`；
   * 确认视觉系统稳定拦截并返回 `ERR,1012`，验证防呆机制在产线真实生效。
