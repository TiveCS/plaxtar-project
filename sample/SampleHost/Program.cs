using SampleHost.Components;
using Plaxtar.Designer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Stand-in "heavy dependency" for the #1 render-gate spike.
builder.Services.AddScoped<SampleHost.Services.ISampleData, SampleHost.Services.SampleData>();

// The Composer package: declare this host's component library + default Shell.
// One call registers the resolver, catalog, source index, session, and screen store.
builder.Services.AddPlaxtarDesigner(o =>
{
    o.AddAssemblyOf<SampleHost.SampleUi.Stack>();
    o.NamespaceFilter = "SampleHost.SampleUi";
    o.DefaultShellName = "SampleLayout";
    o.Fe = "SampleHost";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Plaxtar.Designer.Composer).Assembly);

// #4 dev endpoint: regenerate designs/_catalog.json headlessly (also usable by CI/agents).
app.MapGet("/_catalog/export", async (ComponentCatalog cat, IWebHostEnvironment env) =>
{
    var dir = DesignPaths.DesignsDir(env.ContentRootPath);
    var path = await CatalogManifest.WriteAsync(dir, cat);
    return Results.Ok(new { written = path, count = cat.Components.Count });
});

app.Run();
