---
title: "PackagedIconTintCache"
draft: false
---

## Class `PackagedIconTintCache`

`ASLM/Services/PackagedIconTintCache.cs` — **`internal static`** — builds tinted PNG **`ImageSource`** streams for sidebar icons (bundled asset names or absolute paths). Uses SkiaSharp blend tinting; keyed cache per file + color.

Cleared by [ThemeService](ThemeService/) when palette changes.

---

### Static fields

| Name | Description |
| --- | --- |
| `Gate` | Lock for cache dictionary |
| `Cache` | `Dictionary<string, ImageSource>` (case-sensitive keys) |

---

## Internal methods

#### `internal static void Clear()`

**Purpose:** Clears every cached tinted image so palette changes can rebuild icons.

**Steps:**

1. Under `Gate` → **`Cache.Clear()`**.

---

#### `internal static ImageSource Get(string fileNameOrPath, Color tint)`

**Purpose:** Returns a cached tinted image for the requested file and color, creating it when needed.

**Steps:**

1. Build key: `{fileNameOrPath}\u001f{ColorToKey(tint)}`.
2. Under lock: return existing if present.
3. Else **`CreateImageSource`**, store, return.

---

## Private methods

#### `private static string ColorToKey(Color c)`

**Purpose:** Builds a stable cache key fragment from one tint color.

**Steps:**

1. Invariant format: `Red|Green|Blue|Alpha` (F4 each).

---

#### `private static ImageSource CreateImageSource(string fileNameOrPath, Color tint)`

**Purpose:** Decodes one icon, applies the tint, and returns a stream-backed image source.

**Steps:**

1. **`OpenImageStream`**; if null or decode fails → **`ImageSource.FromFile(fileNameOrPath)`**.
2. Draw source bitmap on RGBA canvas with **`SKColorFilter.CreateBlendMode`** (Modulate).
3. Encode PNG 100%; return **`ImageSource.FromStream`** over byte array.
4. On any exception → fallback **`FromFile`**.

---

#### `private static Stream? OpenImageStream(string fileNameOrPath)`

**Purpose:** Opens the packaged or on-disk PNG stream used as the tint source bitmap.

**Steps:**

1. If rooted path exists → **`File.OpenRead`**.
2. Else try `{BaseDirectory}/tint_png/{fileName}`.
3. Try **`FileSystem.OpenAppPackageFileAsync(fileNameOrPath)`** (sync wait).
4. Else `{BaseDirectory}/{fileNameOrPath}` if exists; else null.

---

## Related

- [IconTintHelper](IconTintHelper/)
- [ThemeService](ThemeService/)
