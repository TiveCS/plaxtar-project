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

app.Run();
