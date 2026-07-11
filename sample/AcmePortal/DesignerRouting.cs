using System.Reflection;

namespace AcmePortal;

// Dev-only gating for the interactive Router (see the Plaxtar.Designer README).
// Debug: the Composer assembly is added so in-circuit nav to /designer resolves.
// Release: the package isn't referenced, so this is empty and no route exists.
public static class DesignerRouting
{
    public static Assembly[] AdditionalAssemblies =>
#if PLAXTAR_DESIGNER
        new[] { typeof(Plaxtar.Designer.Composer).Assembly };
#else
        Array.Empty<Assembly>();
#endif
}
