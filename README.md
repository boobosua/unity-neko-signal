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
- **Priority ordering** — higher priority subscribers are called first; FIFO within the same priority.
- **Subscriber-side filters** — `ISignalFilter` lets the _emitter_ restrict which subscribers receive a signal (e.g. team checks, object-active checks).
- **Fluent filter API** — `signal.ConfigureFilters().Require(f1).Require(f2).Emit()` for one-off filtered emits.
- **Zero allocation on hot paths** — pre-allocate filter arrays; the dispatcher accepts `ISignalFilter[]` directly.
- **Editor tooling** — Signal Tracker window (`Window > Neko Indie > Signal Tracker`) shows live subscribers and emit history.

## Quick Start

### 1. Define a signal

Signals **must** be `struct` — this is enforced at the type-system level. Use `readonly struct` for immutability.

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

```csharp
using NekoSignal;

public class UIHealthBar : MonoBehaviour
{
    private void OnEnable() => SignalHub.Bind(this);
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
Signals.Emit(new PlayerHealthChanged(health, maxHealth));
```

## Usage Examples

### Priority

Higher priority values are invoked first. Default is `0`. Handlers at the same priority are called in subscription order (FIFO).

```csharp
// Attribute-based — runs before default-priority handlers
[OnSignal(priority: 10)]
private void OnHealthChanged(PlayerHealthChanged s) { }

// Programmatic
this.Subscribe<PlayerHealthChanged>(OnHealthChanged, priority: 10);
```

Priority affects dispatch order only. Filters are evaluated per-subscriber regardless of priority.

### Programmatic Subscribe / Unsubscribe

```csharp
public class TemporaryListener : MonoBehaviour
{
    private void OnEnable()  => this.Subscribe<GameStarted>(OnGameStarted);
    private void OnDisable() => this.Unsubscribe<GameStarted>(OnGameStarted);

    private void OnGameStarted(GameStarted s) { }
}
```

### Filtered Emit

Filters run on the _emitter_ side and let you restrict delivery to subscribers whose `MonoBehaviour` owner passes all provided filters. Filters receive the subscriber's owner, not the emitter's.

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
- **Bind/Unbind symmetrically** — call `SignalHub.Bind(this)` in `OnEnable` and `SignalHub.Unbind(this)` in `OnDisable`.
- **Pre-allocate filter arrays on hot paths** — every inline `this.Emit(signal, f1, f2)` call allocates a `params` array. For signals emitted every frame or per-physics-tick, pre-allocate once:

```csharp
private ISignalFilter[] _detectFilters;

private void Awake()
{
    _detectFilters = new ISignalFilter[] { new ActiveFilter(), new TeamFilter(teamId) };
}

private void Update()
{
    if (DetectedEnemy(out var target))
        Signals.Emit(new EnemyDetected(target), _detectFilters); // no allocation
}
```

> **IL2CPP / stripping warning:** `[OnSignal]` handlers are discovered via reflection. If Managed Stripping Level is set to Medium or High, private handler methods may be removed. Set stripping to **Disabled** or preserve handler methods via a `link.xml`.

## Signal Tracker

Open via `Window > Neko Indie > Signal Tracker` to inspect active subscribers and a live emit log with emitter context, payload fields, and applied filters.

## Memory Management

Subscriptions are automatically removed when the owning `MonoBehaviour` or `GameObject` is destroyed. `[OnSignal]` handlers bound via `SignalHub.Bind` are cleaned up automatically.

## Requirements

Unity 6 or later. Requires NekoLib.
