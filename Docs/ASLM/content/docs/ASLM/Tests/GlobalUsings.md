---
title: "GlobalUsings"
draft: false
---

## File `GlobalUsings.cs`

`ASLM/Tests/GlobalUsings.cs` — project-wide usings for every test file.

```csharp
global using FluentAssertions;
global using Xunit;
```

`ASLM.Tests.csproj` also declares `<Using Include="Xunit" />`; assertion and test attributes are available without per-file imports.
