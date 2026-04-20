namespace NekoSignal.Tests
{
    internal struct PingSignal : ISignal { public int Id; }
    internal struct PongSignal : ISignal { public string Message; }
}
