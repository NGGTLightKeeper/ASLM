---
title: "ConsoleOutputView"
draft: false
---

## Class `ConsoleOutputView`

`ASLM/Services/ConsoleOutputView.cs` — **`public sealed`** MAUI **`View`** — hosts bindable state consumed by the native console output handler ([ConsoleOutputViewHandler](ConsoleOutputViewHandler/)).

Used by [ConsolesView](../Pages/ConsolesView/).

No instance methods beyond property accessors; public surface is bindable properties and static `BindableProperty` fields.

---

### Bindable property fields

| Name | Type |
| --- | --- |
| `TextProperty` | `BindableProperty` for `Text` |
| `SessionKeyProperty` | `BindableProperty` for `SessionKey` |

---

## Public properties

#### `public string Text`

**Purpose:** Gets or sets the console text rendered by the native host.

Backed by **`TextProperty`** (default `string.Empty`).

---

#### `public string SessionKey`

**Purpose:** Gets or sets the composite session key used to reset scroll position for a new session.

Backed by **`SessionKeyProperty`** (default `string.Empty`). When the key changes, the handler resets scroll.

---

## Related

- [ConsoleOutputViewHandler](ConsoleOutputViewHandler/) — WinUI `TextBox` host
- [ModuleConsoleStore](ModuleConsoleStore/) — session text source
