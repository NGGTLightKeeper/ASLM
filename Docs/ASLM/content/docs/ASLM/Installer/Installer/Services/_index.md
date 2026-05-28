---
title: "Services"
draft: false
---

Installer business logic:

| Type | Responsibility |
| --- | --- |
| [InstallerService](InstallerService/) | Payload extraction, install layout, manifest, shortcuts, launch |
| [LegalDocumentService](LegalDocumentService/) | Load build-generated legal bundle from app package |

Shared JSON serialization uses internal **`JsonOptions`** (camelCase, indented) in `LegalDocumentService.cs`.
