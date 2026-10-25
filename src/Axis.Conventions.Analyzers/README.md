# Axis.Conventions.Analyzers

Opt-in Roslyn analyzers that turn the conventions of an
[Axis application](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/index.md) into build-time
diagnostics: the shape of handlers, validators, controllers and domain records, the composition root, the error
members and the test names.

```xml
<PackageReference Include="Axis.Conventions.Analyzers" PrivateAssets="all" />
```

Reference it once for every project of the application — in a `Directory.Build.props` — and build with
`-warnaserror`. The package is **separate and opt-in** on purpose: these conventions govern how an application is
built, so they never ride along with an Axis package that a project references for a single primitive.

## Diagnostics

| Id | Category | Flags | Severity |
|---|---|---|---|
| [`AXIS0600`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/analyzers.md#axis0600) | `Axis.Architecture` | a use-case handler that is not `internal sealed` | warning |
| [`AXIS0601`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/analyzers.md#axis0601) | `Axis.Architecture` | a validator (`: AxisValidatorBase<T>`) that is not `internal sealed` | warning |
| [`AXIS0602`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/analyzers.md#axis0602) | `Axis.Edge` | a controller (`: ControllerBase`) that is not `sealed` | warning |
| [`AXIS0603`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/analyzers.md#axis0603) | `Axis.Testing` | a test method in snake_case, or an asynchronous one without the `Async` suffix | info |
| [`AXIS0604`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/analyzers.md#axis0604) | `Axis.Architecture` | a domain properties record (`: I…Properties` of the same project) that is not `internal sealed` | warning |
| [`AXIS0605`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/analyzers.md#axis0605) | `Axis.Edge` | a command property filled from the route (`command with { … }`) that is not `[JsonIgnore]` | warning |
| [`AXIS0606`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/analyzers.md#axis0606) | `Axis.Style` | an entity properties record with two or more constructor parameters sharing a line | warning |
| [`AXIS0607`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/analyzers.md#axis0607) | `Axis.Architecture` | a second `public` `DependencyInjection` class in the same assembly | warning |
| [`AXIS0608`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/analyzers.md#axis0608) | `Axis.Style` | an `AxisError` built at the call site from a `const` code of an errors class | warning |

Each id links to its section of the
[analyzers page](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/analyzers.md): what it
flags, why, and the fix. Detection is **semantic** (implemented interface or base type), except `AXIS0603`, which is
syntactic (the `[Fact]` / `[Theory]` attribute and the method name), and `AXIS0606`, which is semantic on the
interface and syntactic on the parameter layout. Every analyzer does nothing in a project where the Axis type it
looks for is absent.

## Severity

The conventions an application must follow ship as `warning`, the one it should follow (`AXIS0603`) as `info`. Raise
one diagnostic, or a whole category, in the `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.AXIS0600.severity = error
# or a whole concern at once:
dotnet_analyzer_diagnostic.category-Axis.Architecture.severity = error
```

## License

[Apache-2.0](https://github.com/axis-sys/axis-framework-dotnet/blob/main/LICENSE). Released from the
[Axis Framework hub](https://github.com/axis-sys/axis-framework-dotnet), which builds its reference application
with these analyzers.
