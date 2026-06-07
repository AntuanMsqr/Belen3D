# Project rules

## C# naming — NO underscore prefix on private fields

Private/protected fields use plain `camelCase`, **never** an `_` prefix.

```csharp
// ✗ WRONG
private SpatialLayoutService _service;
public void Initialize(SpatialLayoutService service) { _service = service; }

// ✓ RIGHT
private SpatialLayoutService service;
public void Initialize(SpatialLayoutService service) { this.service = service; }
```

Rules:
- Strip the leading `_` from every private/protected field name.
- When a field name collides with a constructor/method parameter or a local, disambiguate with `this.field` (do **not** reintroduce `_`).
- Applies to instance and static fields alike. Static fields qualify with the type name if shadowed, not `this.`.
- Do **not** touch string literals that legitimately start with `_` (e.g. Unity shader properties `"_Color"`, `"_BaseColor"`, `"_MainTex"`).
- Public/serialized fields keep their existing names — renaming a `[SerializeField]`/`public` field breaks scene/prefab/ScriptableObject serialization.
- If stripping `_` would yield a C# keyword (e.g. `_lock` → `lock`), rename to a non-keyword instead (`gate`, `sync`) — never `@lock`.

Constant naming follows existing style (`PascalCase` for `const`/`static readonly`).
