import {
  AssistantRuntimeProvider,
  useLocalRuntime,
  type ChatModelAdapter,
} from "@assistant-ui/react";
import { Thread } from "./thread";

type SseEvent = {
  type: "text" | "tool" | "image" | "error" | "done";
  text?: string;
  name?: string;
  detail?: string;
  url?: string;
  message?: string;
};

function token(): string {
  return new URLSearchParams(window.location.search).get("t") ?? "";
}

function headers(): HeadersInit {
  const t = token();
  return t
    ? { "Content-Type": "application/json", "X-RobotVision-Token": t }
    : { "Content-Type": "application/json" };
}

const adapter: ChatModelAdapter = {
  async *run({ messages, abortSignal }) {
    const payload = {
      messages: messages.map((m) => ({
        role: m.role,
        content: m.content,
      })),
    };
    const response = await fetch("/v1/chat", {
      method: "POST",
      headers: headers(),
      body: JSON.stringify(payload),
      signal: abortSignal,
    });
    if (!response.ok || !response.body)
      throw new Error(await response.text() || `HTTP ${response.status}`);

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";
    let text = "";

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      const parts = buffer.split("\n\n");
      buffer = parts.pop() ?? "";
      for (const part of parts) {
        const line = part.trim();
        if (!line.startsWith("data:")) continue;
        const ev = JSON.parse(line.slice(5).trim()) as SseEvent;
        if (ev.type === "text" && ev.text) text += ev.text;
        else if (ev.type === "tool")
          text += `${text.length > 0 ? "\n" : ""}〔${ev.name}〕${ev.detail ?? ""}`;
        else if (ev.type === "image" && ev.url)
          text += `${text.length > 0 ? "\n" : ""}![](${ev.url})`;
        else if (ev.type === "error")
          text += `${text.length > 0 ? "\n" : ""}${ev.message ?? "请求失败"}`;
        if (text.length > 0) yield { content: [{ type: "text" as const, text }] };
      }
    }
  },
};

export function App() {
  const runtime = useLocalRuntime(adapter);
  return (
    <AssistantRuntimeProvider runtime={runtime}>
      <Thread />
    </AssistantRuntimeProvider>
  );
}
