# VICE Core 0.1.0

Shared .NET 6 library for the owner's VICE family. Install one `VICE.Core.dll` in the game's `UserLibs`, with each module's DLL in `Mods`. Core has no `MelonInfo` and owns no police, cartel or other gameplay hooks. Do not duplicate Core in both directories.

Projects reference `VICE.Core.csproj` and set `UseViceCore=true` to avoid linking the older `src/Shared/ModSupport.cs`. Core references the installed MelonLoader and Harmony assemblies; game-specific modules reference their own IL2CPP assemblies.

The 0.1.0 public contract is:

- `ModSupport.Patch(Harmony, Type target, string method, Type owner, string? prefix=null, string? postfix=null, Type[]? parameters=null, string? finalizer=null)` returns whether the hook was installed and logs missing targets once.
- `ModSupport.Entry<T>(category, name, value, description)` creates a Melon preference.
- `ModSupport.MigratedEntry<T>(category, name, value, description, legacyCategory)` supplies the old category's value as the initial default; an existing new-category value wins.
- `ModSupport.WarnOnce(key, message)` avoids repeated warnings across VICE modules.
- `SubscriptionScope.Add(Action release)` registers cleanup; `Dispose()` invokes releases in reverse order, continues after an invalid native owner, and permits reuse for another loaded save.
- `DefaultFiles.Seed(Assembly, prefix, directory)` writes embedded defaults only where no file exists and rejects paths outside the destination.

These signatures are shared with VICE.Syndicate. Retain them when adding more services. Future modules should avoid owning the same native hooks or distributing a separate embedded copy of Core. The legacy personal modules remain independent until individually migrated.
