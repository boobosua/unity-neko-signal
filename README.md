# Neko Signal

A lightweight, type-safe event system for Unity that provides decoupled communication between game objects and systems.

## Installation

### Via Git URL

1. Install NekoLib package first by adding this via Unity Package Manager:

```
https://github.com/boobosua/unity-nekolib.git
```

2. Add this package via Unity Package Manager:

```
https://github.com/boobosua/unity-neko-signal.git
```

## Features

## What is NekoSignal?

NekoSignal is a Unity package for sending and receiving strongly-typed signals (events) between scripts. It supports attribute-based handlers, automatic binding, and flexible filtering for signal delivery.

## Quick Start

## Best Practices

- Define signals as `public readonly struct` implementing `ISignal` for immutability and performance.
- Define filters as `public sealed class` implementing `ISignalFilter` if you do not need to extend them further.
- Use `[OnSignal]` on private methods for signal handlers and bind with `SignalHub.Bind(this)` in `OnEnable`.

1. **Create a signal structure**:

```csharp
public readonly struct PlayerHealthChanged : ISignal
{
    public int newHealth;
    public int maxHealth;
}
```

2. **Attribute-based subscription**:

```csharp
using NekoSignal;

public class UIHealthBar : MonoBehaviour
{
    [OnSignal]
    private void OnHealthChanged(PlayerHealthChanged signal)
    {
        healthBar.fillAmount = (float)signal.newHealth / signal.maxHealth;
    }

    private void OnEnable()
    {
        SignalHub.Bind(this); // Automatically binds all [OnSignal] methods
    }
}
```

3. **Publish signals with filters**:

```csharp
using NekoSignal;

public class Player : MonoBehaviour
{
    private void TakeDamage(int damage)
    {
        health -= damage;
        // Publish with filters (shorthand)
        this.Publish(
            new PlayerHealthChanged { newHealth = health, maxHealth = maxHealth },
            new OwnerIsActiveFilter(),
            new CustomTeamFilter(teamId)
        );
    }
}
```

## Usage Examples

### Creating Custom Signal Filters

You can extend `ISignalFilter` to create your own logic for filtering which subscribers receive a published signal. This is useful for targeting specific listeners based on custom rules (e.g., team membership, object state, etc).

**Example: Only allow listeners on a specific team to receive the signal**

```csharp
using NekoSignal;
using UnityEngine;

public sealed class TeamFilter : ISignalFilter
{
    private readonly int _teamId;

    public TeamFilter(int teamId)
    {
        _teamId = teamId;
    }

    public bool Evaluate(MonoBehaviour owner)
    {
        // Example: Assume owner has a TeamMember component
        var member = owner.GetComponent<TeamMember>();
        return member != null && member.TeamId == _teamId;
    }
}

// Usage when publishing a signal (shorthand):
new PlayerHealthChanged { newHealth = health, maxHealth = maxHealth }
    .SetEmitter(this)
    .Require(new TeamFilter(teamId))
    .Publish();
```

### Creating Signals

Signals are simple data structures implementing `ISignal`:

```csharp
// Simple event signal
public readonly struct GameStarted : ISignal { }

// Signal with data
public readonly struct PlayerHealthChanged : ISignal
{
    public int newHealth;
    public int maxHealth;
}

// Complex signal with multiple properties
public readonly struct ItemCollected : ISignal
{
    public string itemId;
    public Vector3 position;
    public ItemType type;
    public int quantity;
}
```

### Subscribing and Publishing

```csharp
using NekoSignal;

public class Player : MonoBehaviour
{
    private void OnEnable()
    {
        SignalHub.Bind(this); // Binds all [OnSignal] methods
    }

    [OnSignal]
    private void OnGameStarted(GameStarted signal)
    {
        InitializePlayer();
    }

    [OnSignal(typeof(ItemCollected))] // Explicit signal type
    private void HandleItem(object signal)
    {
        var item = (ItemCollected)signal;
        ShowItemPopup(item.itemId, item.position);
    }

    private void TakeDamage(int damage)
    {
        health -= damage;

        // Publish with filters (shorthand)
        this.Publish(
            new PlayerHealthChanged { newHealth = health, maxHealth = maxHealth },
            new OwnerIsActiveFilter(),
            new CustomTeamFilter(teamId)
        );
    }
}
```

### Manual Unsubscription

```csharp
public class TemporaryListener : MonoBehaviour
{
    private Action<GameStarted> gameStartedHandler;

    private void OnEnable()
    {
        gameStartedHandler = OnGameStarted;
        // Subscribe (shorthand)
        this.Subscribe<GameStarted>(gameStartedHandler);
    }

    private void SomeCondition()
    {
        // Manual unsubscribe when needed
        this.Unsubscribe<GameStarted>(gameStartedHandler);
    }

    private void OnGameStarted(GameStarted signal)
    {
        // Handle game start
    }
}
```

## Signal Tracker

You can open the Signal Tracker window in Unity via `Tools > Neko Indie > Signal Tracker` to view active signals and subscribers.

## Memory Management

Signal subscriptions are automatically cleaned up when GameObjects are destroyed. `[OnSignal]` methods are auto-unsubscribed.

## Requirements

Requires Unity 2020.3 or later and NekoLib.

---
