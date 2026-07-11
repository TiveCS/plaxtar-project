# Plaxtar.Designer

Dev-only, in-process visual **Composer** for Blazor. Assemble your already-coded
components into screens, render them live inside the real Shell, and export a
codegen-grade Design Tree (`plaxtar.designer/v1`) that an AI agent turns into real
`.razor` pages.

It runs at a dev-only route **you choose** inside your own app, so it renders against
your real DI, auth, and layout — no isolated canvas, no component redraw, no drift.

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

### 2. Register the Composer's services, guarded

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
```

No `AddAdditionalAssemblies` and no `<Router>` indirection: the Composer is a plain
component, and *your own* host page (step 3) owns the route, so it's discovered like any
other page in your app.

### 3. Host the Composer at a route you choose

Add a dev-only page anywhere in your app — pick whatever path you like
(`/designer`, `/_plaxtar/_designer`, `/admin/designer`, …). The Composer carries its own
interactive render mode, so the page needs nothing else:

```razor
@* Components/Pages/Designer.razor *@
@page "/_plaxtar/_designer"
@using Plaxtar.Designer

<Composer />
```

Exclude that page from Release so no route ships (it references the dev-only package):

```xml
<!-- YourFrontend.csproj -->
<ItemGroup Condition="'$(Configuration)' != 'Debug'">
  <Content Remove="Components\Pages\Designer.razor" />
</ItemGroup>
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
