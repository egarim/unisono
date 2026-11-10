# Unisono.AppIntents.Binding

Objective-C binding of the generic Swift bridge used by [Unisono](https://github.com/egarim/unisono)
(`UnisonoHost` protocol and `UnisonoBridge` singleton). It is written once and never changes per app, because the
bridge exchanges JSON. You normally do not reference it directly: `Unisono.AppIntents` depends on it.

```bash
dotnet add package Unisono.AppIntents --prerelease
```

Documentation: https://github.com/egarim/unisono
