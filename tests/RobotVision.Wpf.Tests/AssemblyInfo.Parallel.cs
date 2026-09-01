using Xunit;

// WPF 测试共享全局状态：Application.Current 单例、Dispatcher、全局日志管道（LogSink 订阅）。
// xunit 默认并行会互相污染（样式链被改、日志串流），必须在程序集级别完全禁用并行。
// 单个测试类内的测试仍按声明顺序串行执行，无性能顾虑（全量 <10s）。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
