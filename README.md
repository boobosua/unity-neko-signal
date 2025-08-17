# NekoSignal

A lightweight, type-safe event system for Unity that provides decoupled communication between game objects and systems.

## Table of Contents

- [Installation](#installation)
- [Features](#features)
- [Quick Start](#quick-start)
- [Usage Examples](#usage-examples)
  - [Creating Signals](#creating-signals)
  - [Subscribing and Publishing](#subscribing-and-publishing)
  - [Manual Unsubscription](#manual-unsubscription)
- [Signal Tracker](#signal-tracker)
- [Memory Management](#memory-management)
- [Requirements](#requirements)

## Installation

### Via Git URL

Add this package via Unity Package Manager:

```
https://github.com/boobosua/unity-neko-signal.git
```

### Manual Installation

1. Download the package
2. Import into your Unity project
3. No external dependencies required

## Features

- **Type-Safe**: Compile-time signal type checking
- **Automatic Cleanup**: Signals are automatically unsubscribed when GameObjects are destroyed
- **Lambda Support**: Subscribe with lambda expressions for quick event handling
- **Memory Efficient**: No boxing/unboxing, minimal allocation overhead
- **Editor Tools**: Built-in Signal Tracker window for debugging and monitoring
- **Thread-Safe**: Safe to use from any thread (publishing happens on main thread)

## Quick Start

1. **Create a signal structure**:

```csharp
public struct PlayerHealthChanged : ISignal
{
    public int newHealth;
    public int maxHealth;
}
```

2. **Subscribe to signals**:

```csharp
public class UIHealthBar : MonoBehaviour
{
    private void OnEnable()
    {
        this.Subscribe<PlayerHealthChanged>(OnHealthChanged);
    }

    private void OnHealthChanged(PlayerHealthChanged signal)
    {
        // Update health bar UI
        healthBar.fillAmount = (float)signal.newHealth / signal.maxHealth;
    }
}
```

3. **Publish signals**:

```csharp
public class Player : MonoBehaviour
{
    private void TakeDamage(int damage)
    {
        health -= damage;
        this.Publish(new PlayerHealthChanged
        {
            newHealth = health,
            maxHealth = maxHealth
        });
    }
}
```

## Usage Examples

### Creating Signals

Signals are simple data structures implementing `ISignal`:

```csharp
// Simple event signal
public struct GameStarted : ISignal { }

// Signal with data
public struct PlayerHealthChanged : ISignal
{
    public int newHealth;
    public int maxHealth;
}

// Complex signal with multiple properties
public struct ItemCollected : ISignal
{
    public string itemId;
    public Vector3 position;
    public ItemType type;
    public int quantity;
}
```

### Subscribing and Publishing

```csharp
public class Player : MonoBehaviour
{
    private void OnEnable()
    {
        // Subscribe to signals
        this.Subscribe<GameStarted>(OnGameStarted);

        // Lambda expressions for simple logic
        this.Subscribe<ItemCollected>(signal =>
        {
            ShowItemPopup(signal.itemId, signal.position);
        });
    }

    private void TakeDamage(int damage)
    {
        health -= damage;

        // Publish signals
        this.Publish(new PlayerHealthChanged
        {
            newHealth = health,
            maxHealth = maxHealth
        });
    }

    private void OnGameStarted(GameStarted signal)
    {
        InitializePlayer();
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

Monitor and debug your signals in real-time:

1. **Open the Signal Tracker**: `Tools > Neko Indie > Signal Tracker`
2. **View active signals**: See all signal types and their subscribers
3. **Real-time monitoring**: Auto-refresh shows live subscription changes
4. **Debug features**:
   - Click GameObjects to select them in hierarchy
   - See method names and lambda expressions
   - Clear all signals for testing
   - Filter and search signals

**Signal Tracker Features**:

- 📊 Live subscriber count and status
- 🔍 Search and filter capabilities
- 🎯 Clickable GameObjects for quick navigation
- 🧹 Manual cleanup tools
- ⚡ Real-time refresh with configurable rate

## Memory Management

NekoSignal handles memory management automatically:

- **Automatic Cleanup**: Signals are unsubscribed when GameObjects are destroyed
- **Lambda Handling**: Lambda expressions are properly tracked and cleaned up
- **No Memory Leaks**: Built-in protection against common event system pitfalls
- **Efficient Storage**: Uses multicast delegates for optimal performance

**Best Practices**:

- Subscribe in `OnEnable`, unsubscribe happens automatically
- Use lambda expressions for simple, local event handling
- Use method references for complex logic that might be reused
- Monitor with Signal Tracker during development

## Requirements

- Unity 2020.3 or later
- Instal NekoLib library

---
