import {
  ComposerPrimitive,
  MessagePrimitive,
  ThreadPrimitive,
} from "@assistant-ui/react";

export function Thread() {
  return (
    <ThreadPrimitive.Root className="rv-thread">
      <ThreadPrimitive.Viewport className="rv-viewport">
        <ThreadPrimitive.Empty>
          <div className="rv-empty">
            <h1>本机视觉调试助手</h1>
            <p>面向光模块装配引导。先读本机实况，再给结论；不编造产量、坐标与结果码。</p>
            <ul>
              <li>站况 — 相机是否可取图、配方与标定档案、TCP 与检测队列</li>
              <li>分析 — 今日合格率、失败码、角度离散、按配方对比</li>
              <li>调试 — 拍照或按配方试跑；删除与停 TCP 请写明对象</li>
            </ul>
          </div>
        </ThreadPrimitive.Empty>
        <ThreadPrimitive.Messages>
          {({ message }) =>
            message.role === "user" ? (
              <div className="rv-bubble rv-user">
                <MessagePrimitive.Root>
                  <MessagePrimitive.Parts />
                </MessagePrimitive.Root>
              </div>
            ) : (
              <div className="rv-bubble rv-assistant">
                <MessagePrimitive.Root>
                  <MessagePrimitive.Parts />
                </MessagePrimitive.Root>
              </div>
            )
          }
        </ThreadPrimitive.Messages>
        <ThreadPrimitive.ViewportFooter>
          <ComposerPrimitive.Root className="rv-composer">
            <ComposerPrimitive.Input
              className="rv-input"
              placeholder="例如：今日合格率、相机能否取图、解除 1018 联锁"
              rows={1}
            />
            <ComposerPrimitive.If running={false}>
              <ComposerPrimitive.Send className="rv-send">发</ComposerPrimitive.Send>
            </ComposerPrimitive.If>
            <ComposerPrimitive.If running>
              <ComposerPrimitive.Cancel className="rv-stop">停止</ComposerPrimitive.Cancel>
            </ComposerPrimitive.If>
          </ComposerPrimitive.Root>
        </ThreadPrimitive.ViewportFooter>
      </ThreadPrimitive.Viewport>
    </ThreadPrimitive.Root>
  );
}
