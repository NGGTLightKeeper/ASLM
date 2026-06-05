---
title: "TestLoggerFactory"
draft: false
---

## Class `TestLoggerFactory`

`ASLM/Tests/TestSupport/TestLoggerFactory.cs` — **`public static`** — silent `ILogger<T>` for services that require logging in unit tests.

---

## Public methods
#### `public static ILogger<T> Create<T>()`

**Purpose:** Creates a logger via `LoggerFactory` + internal **`NullLoggerProvider`** (no output, `IsEnabled` always false).

---

## Nested `NullLoggerProvider` (`private sealed`)

#### `public ILogger CreateLogger(string categoryName)`

**Purpose:** Returns **`NullLogger`** instance.

---

#### `public void Dispose()`

**Purpose:** No-op.

---

## Nested `NullLogger` (`private sealed`)

#### `public IDisposable? BeginScope<TState>(TState state)`

**Purpose:** Returns `null`.

---

#### `public bool IsEnabled(LogLevel logLevel)`

**Purpose:** Always `false`.

---

#### `public void Log<TState>(LogLevel, EventId, TState, Exception?, Func<TState, Exception?, string>)`

**Purpose:** No-op.

---

## Related

- [AslmFileSystemLayout](AslmFileSystemLayout/)
