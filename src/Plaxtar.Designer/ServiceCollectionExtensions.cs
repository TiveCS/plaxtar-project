using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Plaxtar.Designer;

// One-call registration for the Composer. The host wires its component library and
// (optionally) a default Shell; everything the Composer needs is registered here.
//
//   builder.Services.AddPlaxtarDesigner(o =>
//   {
//       o.AddAssemblyOf<MyButton>();
//       o.NamespaceFilter = "MyApp.Base.UI";
//       o.DefaultShellName = "MainLayout";
//   });
//
// Route the Composer page by adding this assembly to the Router's
// AdditionalAssemblies: typeof(Plaxtar.Designer.Composer).Assembly.
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlaxtarDesigner(
        this IServiceCollection services, Action<PlaxtarDesignerOptions> configure)
    {
        var options = new PlaxtarDesignerOptions();
        configure(options);
        services.AddSingleton(options);

        services.AddSingleton(new ComponentTypeResolver(options.ComponentAssemblies));
        services.AddSingleton(sp => new RazorSourceIndex(
            sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath));
        services.AddSingleton(sp => new ComponentCatalog(
            options.ComponentAssemblies,
            sp.GetRequiredService<RazorSourceIndex>(),
            options.NamespaceFilter));

        services.AddScoped<ScreenLoader>();
        services.AddScoped<DesignSession>();
        services.AddSingleton<ScreenStore>();

        return services;
    }
}
