using AcmePortal.Components;
#if PLAXTAR_DESIGNER
using Plaxtar.Designer;
#endif

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The app's own "heavy" dependencies (auth session, API/DB access). The Shell
// consumes IAuthContext, so the Composer must render inside this real container.
builder.Services.AddScoped<AcmePortal.Services.IAuthContext, AcmePortal.Services.FakeAuthContext>();
builder.Services.AddScoped<AcmePortal.Services.ICustomerData, AcmePortal.Services.FakeCustomerData>();

// Plaxtar Composer — dev-only. AcmePortal has no other knowledge of Plaxtar; this
// is the entire integration surface: declare the component library + default Shell.
#if PLAXTAR_DESIGNER
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddPlaxtarDesigner(o =>
    {
        o.AddAssemblyOf<AcmePortal.Ui.PrimaryButton>();
        o.NamespaceFilter = "AcmePortal.Ui";
        o.DefaultShellName = "PortalLayout";
        o.Fe = "AcmePortal";
    });
}
#endif

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

#if PLAXTAR_DESIGNER
if (app.Environment.IsDevelopment())
{
    // The Composer host page lives in this app (Components/Pages/Designer.razor), so the
    // router discovers it normally — no AddAdditionalAssemblies needed.

    app.MapGet("/_catalog/export", async (ComponentCatalog cat, IWebHostEnvironment env, PlaxtarDesignerOptions opt) =>
    {
        var dir = DesignPaths.DesignsDir(env.ContentRootPath, opt.DesignsPath);
        var path = await CatalogManifest.WriteAsync(dir, cat);
        return Results.Ok(new { written = path, count = cat.Components.Count });
    });
}
#endif

app.Run();
