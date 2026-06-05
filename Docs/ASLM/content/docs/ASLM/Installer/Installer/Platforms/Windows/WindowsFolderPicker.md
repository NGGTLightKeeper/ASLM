---
title: "WindowsFolderPicker"
draft: false
---

## Class `WindowsFolderPicker`

`ASLM/Installer/Installer/Platforms/Windows/WindowsFolderPicker.cs` — **`internal static`** — native folder picker via COM **`IFileOpenDialog`** (not WinRT).

---

### Constants / fields

| Name | Value |
| --- | --- |
| `OperationCanceled` | `HRESULT 0x800704C7` |
| `FileOpenDialogId` | CLSID `DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7` |

---

## Member reference
#### `public static string? PickFolder(object? ownerWindow, string title)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `Activator.CreateInstance(Type.GetTypeFromCLSID(FileOpenDialogId))` as **`IFileOpenDialog`** |
| 2 | `GetOptions` / `SetOptions`: `PickFolders`, `ForceFileSystem`, `PathMustExist`, `NoChangeDir` |
| 3 | `SetTitle(title)` |
| 4 | `Show(GetOwnerHandle(ownerWindow))` |
| 5 | If `OperationCanceled` → 
ull` |
| 6 | Else `Marshal.ThrowExceptionForHR(result)` |
| 7 | `GetResult` → `IShellItem.GetDisplayName(FileSystemPath)` |
| 8 | `Marshal.PtrToStringUni`; `FreeCoTaskMem` in `finally` |

Called from [MainPage](../../MainPage/) **`OnBrowseClicked`** (`#if WINDOWS`).

---

#### `private static IntPtr GetOwnerHandle(object? ownerWindow)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | If `ownerWindow` is null → `IntPtr.Zero` |
| 2 | Try `WindowNative.GetWindowHandle(ownerWindow)` |
| 3 | On failure → `IntPtr.Zero` |

---

## COM interop (private)

### Interface `IFileOpenDialog`

`[ComImport]` / GUID `D57C7288-D4AD-4768-BE02-9D969532D960` / `IUnknown`.

| Method | Used by picker |
| --- | --- |
| `Show(IntPtr owner)` | Yes — `[PreserveSig]` HRESULT |
| `SetFileTypes` | No |
| `SetFileTypeIndex` | No |
| `GetFileTypeIndex` | No |
| `Advise` | No |
| `Unadvise` | No |
| `SetOptions` | Yes |
| `GetOptions` | Yes |
| `SetDefaultFolder` | No |
| `SetFolder` | No |
| `GetFolder` | No |
| `GetCurrentSelection` | No |
| `SetFileName` | No |
| `GetFileName` | No |
| `SetTitle` | Yes |
| `SetOkButtonLabel` | No |
| `SetFileNameLabel` | No |
| `GetResult` | Yes |
| `AddPlace` | No |
| `SetDefaultExtension` | No |
| `Close` | No |
| `SetClientGuid` | No |
| `ClearClientData` | No |
| `SetFilter` | No |
| `GetResults` | No |
| `GetSelectedItems` | No |

### Interface `IShellItem`

GUID `43826D1E-E718-42EE-BC55-A1E261C37BFE`.

| Method | Used by picker |
| --- | --- |
| `BindToHandler` | No |
| `GetParent` | No |
| `GetDisplayName` | Yes — `ShellItemDisplayName.FileSystemPath` |
| `GetAttributes` | No |
| `Compare` | No |

### Enum `FileOpenOptions` (`[Flags]`)

| Flag | Value | Used |
| --- | --- | --- |
| `NoChangeDir` | `0x8` | Yes |
| `PickFolders` | `0x20` | Yes |
| `ForceFileSystem` | `0x40` | Yes |
| `PathMustExist` | `0x800` | Yes |

### Enum `ShellItemDisplayName`

| Member | Value | Used |
| --- | --- | --- |
| `FileSystemPath` | `0x80058000` | Yes |

---

## Related

- [App (Windows)](App/)
- [MainPage](../../MainPage/)
