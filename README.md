# NekoSignal

A lightweight, type-safe signal (event) bus for Unity. Supports attribute-based handlers, priority ordering, and subscriber-side filtering — with zero reflection overhead at emit time.

## Installation

### Via Git URL

1. Install NekoLib first via Unity Package Manager:

```
https://github.com/boobosua/unity-nekolib.git
```

2. Then add NekoSignal:

```
https://github.com/boobosua/unity-neko-signal.git
```

## Features

- **Struct-only signals** — `ISignal` is constrained to `struct`, so signals are always stack-allocated value types; null payload is impossible by design.
- **Attribute binding** — decorate methods with `[OnSignal]` and call `SignalHub.Bind(this)` once; no manual wiring.
- **Manual subscriptions** — `Listen<T>()` returns a `SignalReceiver`; call `Dispose()` to unsubscribe at any time.
- **Priority ordering** — higher priority subscribers are called first; FIFO within the same priority.
- **Emitter-side filters** — `ISignalFilter` lets the emitter restrict which subscribers receive a signal (e.g. team checks, object-active checks).
- **Fluent filter API** — `signal.ConfigureFilters().Require(f1).Require(f2).Emit()` for one-off filtered emits.
- **Zero allocation on hot paths** — pre-allocate filter arrays; the dispatcher accepts `ISignalFilter[]` directly.
- **Editor tooling** — Signal Tracker window (`Window > Neko Framework > Signal Tracker`) with live subscription monitor, emit log, and memory leak detector.

## Quick Start

### 1. Define a signal

Signals **must** be `struct` — enforced at the type-system level. Use `readonly struct` for immutability.

```csharp
public readonly struct PlayerDied : ISignal { }

public readonly struct PlayerHealthChanged : ISignal
{
    public readonly int NewHealth;
    public readonly int MaxHealth;

    public PlayerHealthChanged(int newHealth, int maxHealth)
    {
        NewHealth = newHealth;
        MaxHealth = maxHealth;
    }
}
```

### 2. Subscribe with `[OnSignal]`

Decorate handler methods with `[OnSignal]`, then call `SignalHub.Bind(this)` / `SignalHub.Unbind(this)`. NekoSignal discovers all matching methods via reflection at bind time.

```csharp
using NekoSignal;

public class UIHealthBar : MonoBehaviour
{
    private void OnEnable()  => SignalHub.Bind(this);
    private void OnDisable() => SignalHub.Unbind(this);

    [OnSignal]
    private void OnHealthChanged(PlayerHealthChanged s)
    {
        healthBar.fillAmount = (float)s.NewHealth / s.MaxHealth;
    }
}
```

### 3. Emit

From a `MonoBehaviour` (records the emitter for the Signal Tracker):

```csharp
this.Emit(new PlayerHealthChanged(health, maxHealth));
```

From anywhere (no emitter context):

```csharp
SignalBus.Emit(new PlayerHealthChanged(health, maxHealth));
```

## Usage Examples

### Priority

Higher priority values are invoked first. Default is `0`. Handlers at the same priority are called in subscription order (FIFO).

```csharp
// Attribute-based — runs before default-priority handlers
[OnSignal(priority: 10)]
private void OnHealthChanged(PlayerHealthChanged s) { }

// Manual subscription with priority
SignalReceiver _receiver;
_receiver = this.Listen<PlayerHealthChanged>(OnHealthChanged, priority: 10);
```

Priority affects dispatch order only. Filters are evaluated per-subscriber regardless of priority.

### Manual Subscribe with `Listen`

Use `Listen` when you need to subscribe outside of `OnEnable/OnDisable`, conditionally, or for a limited lifetime. It returns a `SignalReceiver` — call `Dispose()` to unsubscribe.

```csharp
public class TemporaryListener : MonoBehaviour
{
    private SignalReceiver _receiver;

    private void OnEnable()
    {
        _receiver = this.Listen<GameStarted>(OnGameStarted);
    }

    private void OnDisable()
    {
        _receiver.Dispose();
    }

    private void OnGameStarted(GameStarted s) { }
}
```

`SignalReceiver.Dispose()` is idempotent — safe to call multiple times. You can also subscribe from anywhere via `SignalBus.Listen<T>(owner, callback)`.

### Filtered Emit

Filters run on the emitter side and restrict delivery to subscribers whose `MonoBehaviour` owner passes all provided filters.

**One-off (fluent):**

```csharp
new EnemySpotted(target)
    .ConfigureFilters()
    .Require(new TeamFilter(teamId))
    .Require(new ActiveFilter())
    .Emit();
```

**Direct (inline):**

```csharp
this.Emit(new EnemySpotted(target), new TeamFilter(teamId), new ActiveFilter());
```

### Creating a Custom Filter

```csharp
public sealed class TeamFilter : ISignalFilter
{
    private readonly int _teamId;
    public TeamFilter(int teamId) => _teamId = teamId;

    public bool Evaluate(MonoBehaviour owner)
    {
        var member = owner.GetComponent<TeamMember>();
        return member != null && member.TeamId == _teamId;
    }
}
```

## Best Practices

- **`readonly struct` for signals** — immutable, stack-allocated, zero GC. Never use a class.
- **`sealed class` for filters** — prevents accidental inheritance; no virtual dispatch overhead.
- **Bind/Unbind symmetrically** — always pair `SignalHub.Bind(this)` in `OnEnable` with `SignalHub.Unbind(this)` in `OnDisable`. Forgetting `Unbind` leaks the delegate; the Memory Leaks tab in Signal Tracker will surface it.
- **Dispose `Listen` receivers** — store the returned `SignalReceiver` and call `Dispose()` when done. Abandoned receivers are automatically cleaned up when the owner `MonoBehaviour` is destroyed, but explicit disposal is cleaner.
- **Pre-allocate filter arrays on hot paths** — every inline `this.Emit(signal, f1, f2)` call allocates a `params` array. For signals emitted every frame, pre-allocate once:

```csharp
private ISignalFilter[] _detectFilters;

private void Awake()
{
    _detectFilters = new ISignalFilter[] { new ActiveFilter(), new TeamFilter(teamId) };
}

private void Update()
{
    if (DetectedEnemy(out var target))
        SignalBus.Emit(new EnemyDetected(target), _detectFilters); // no allocation
}
```

> **IL2CPP / stripping warning:** `[OnSignal]` handlers are discovered via reflection. If Managed Stripping Level is Medium or High, private handler methods may be stripped. Set stripping to **Disabled** or preserve them via a `link.xml`.

## Signal Tracker

Open via `Window > Neko Framework > Signal Tracker`.

| Tab                      | What it shows                                                                                                                          |
| ------------------------ | -------------------------------------------------------------------------------------------------------------------------------------- |
| **Subscription Monitor** | Live subscriber table per signal type — GameObject, component, method, and priority. Searchable.                                       |
| **Signal Log**           | Emit history with emitter context, timestamp, payload fields, and applied filters. Configurable capacity.                              |
| **Memory Leaks**         | MonoBehaviours that called `SignalHub.Bind` but were destroyed without calling `Unbind`. Cleared automatically when exiting Play Mode. |

## Memory Management

There are two subscription paths with different lifetime rules:

| Path             | How to subscribe                             | How to unsubscribe                                                          |
| ---------------- | -------------------------------------------- | --------------------------------------------------------------------------- |
| `SignalHub.Bind` | Discovers `[OnSignal]` methods automatically | Must call `SignalHub.Unbind` — not automatic                                |
| `Listen<T>`      | Returns a `SignalReceiver`                   | Call `receiver.Dispose()`, or it auto-cleans when the owner MB is destroyed |

Forgetting `SignalHub.Unbind` keeps the delegate alive indefinitely. Check the **Memory Leaks** tab in Signal Tracker during Play Mode to detect these.

## Requirements

Unity 6 or later. Requires NekoLib.
