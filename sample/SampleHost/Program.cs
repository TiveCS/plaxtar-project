using SampleHost.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Stand-in "heavy dependency" for the #1 render-gate spike.
builder.Services.AddScoped<SampleHost.Services.ISampleData, SampleHost.Services.SampleData>();

// #2 walking skeleton: resolve components by name + load design trees from JSON.
builder.Services.AddSingleton(new SampleHost.Designer.ComponentTypeResolver(
    new[] { typeof(SampleHost.SampleUi.Stack).Assembly }));
builder.Services.AddScoped<SampleHost.Designer.ScreenLoader>();

// #4 Catalog: reflect the component library + index .razor sources for the manifest.
builder.Services.AddSingleton(sp => new SampleHost.Designer.RazorSourceIndex(
    sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath));
builder.Services.AddSingleton(sp => new SampleHost.Designer.ComponentCatalog(
    new[] { typeof(SampleHost.SampleUi.Stack).Assembly },
    sp.GetRequiredService<SampleHost.Designer.RazorSourceIndex>(),
    namespaceFilter: "SampleHost.SampleUi"));

// #5 composer: per-circuit editing state.
builder.Services.AddScoped<SampleHost.Designer.DesignSession>();

// #8 screens: list/manage screen files.
builder.Services.AddSingleton<SampleHost.Designer.ScreenStore>();

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
    .AddInteractiveServerRenderMode();

// #4 dev endpoint: regenerate designs/_catalog.json headlessly (also usable by CI/agents).
app.MapGet("/_catalog/export", async (SampleHost.Designer.ComponentCatalog cat, IWebHostEnvironment env) =>
{
    var dir = SampleHost.Designer.DesignPaths.DesignsDir(env.ContentRootPath);
    var path = await SampleHost.Designer.CatalogManifest.WriteAsync(dir, cat);
    return Results.Ok(new { written = path, count = cat.Components.Count });
});

app.Run();
