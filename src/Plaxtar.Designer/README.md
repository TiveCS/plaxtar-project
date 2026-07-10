# Plaxtar.Designer

Dev-only, in-process visual **Composer** for Blazor. Assemble your already-coded
components into screens, render them live inside the real Shell, and export a
codegen-grade Design Tree (`plaxtar.designer/v1`) that an AI agent turns into real
`.razor` pages.

It runs as a route (`/designer`) inside your own app, so it renders against your
real DI, auth, and layout — no isolated canvas, no component redraw, no drift.

## Install (dev-only gating)

The Composer must **never reach production**. Gate it in three layers so a Release
build contains no `Plaxtar.Designer` assembly and no `/designer` route.

### 1. Reference the package in `Debug` only

```xml
<!-- YourFrontend.csproj -->
<ItemGroup Condition="'$(Configuration)' == 'Debug'">
  <ProjectReference Include="..\..\src\Plaxtar.Designer\Plaxtar.Designer.csproj" />
  <!-- or: <PackageReference Include="Plaxtar.Designer" Version="x.y.z" /> -->
</ItemGroup>

<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <DefineConstants>$(DefineConstants);PLAXTAR_DESIGNER</DefineConstants>
</PropertyGroup>
```

The `PLAXTAR_DESIGNER` constant lets the wiring below compile out in Release, where
the package isn't referenced at all.

### 2. Register + route the Composer, guarded

```csharp
// Program.cs
#if PLAXTAR_DESIGNER
using Plaxtar.Designer;
#endif

// ...
#if PLAXTAR_DESIGNER
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddPlaxtarDesigner(o =>
    {
        o.AddAssemblyOf<MyButton>();       // your component library assembly/assemblies
        o.NamespaceFilter = "MyApp.Base.UI"; // optional: only surface library components
        o.DefaultShellName = "MainLayout";   // optional: default @layout for new screens
        o.Fe = "UI.Audit";                   // optional: micro-frontend id written to exports
    });
}
#endif

var app = builder.Build();

var razorComponents = app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

#if PLAXTAR_DESIGNER
if (app.Environment.IsDevelopment())
    razorComponents.AddAdditionalAssemblies(typeof(Composer).Assembly);
#endif
```

> **Blazor Web App note:** server-side route discovery needs
> `MapRazorComponents(...).AddAdditionalAssemblies(...)`. The `<Router>`'s
> `AdditionalAssemblies` alone is not enough — the `/designer` endpoint 404s without it.

### 3. (Optional) interactive router discovery

For in-circuit navigation to `/designer`, add the assembly to the `<Router>` too —
via an indirection so `Routes.razor` has no hard reference to the package in Release:

```csharp
// DesignerRouting.cs
public static class DesignerRouting
{
    public static Assembly[] AdditionalAssemblies =>
#if PLAXTAR_DESIGNER
        new[] { typeof(Plaxtar.Designer.Composer).Assembly };
#else
        Array.Empty<Assembly>();
#endif
}
```

```razor
<Router AppAssembly="typeof(Program).Assembly"
        AdditionalAssemblies="MyApp.DesignerRouting.AdditionalAssemblies">
```

## Verify prod-safety

`scripts/verify-prod-safety.ps1` publishes a Release build and asserts: no designer
assembly in the output, `/designer` → 404, `/_catalog/export` → 404. Wire it into CI.

```powershell
pwsh ./scripts/verify-prod-safety.ps1
```

## Export the Catalog

`GET /_catalog/export` (dev-only) writes `designs/_catalog.json` — the manifest the
agent reads alongside design trees to resolve param types and import paths.
