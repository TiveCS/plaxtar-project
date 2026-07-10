using System.Reflection;

namespace SampleHost;

// Dev-only gating (#10) for the interactive Router. In Debug the Composer assembly
// is added so in-circuit navigation to /designer resolves; in Release the package
// isn't referenced at all, so this is an empty array and no /designer route exists.
public static class DesignerRouting
{
    public static Assembly[] AdditionalAssemblies =>
#if PLAXTAR_DESIGNER
        new[] { typeof(Plaxtar.Designer.Composer).Assembly };
#else
        Array.Empty<Assembly>();
#endif
}
