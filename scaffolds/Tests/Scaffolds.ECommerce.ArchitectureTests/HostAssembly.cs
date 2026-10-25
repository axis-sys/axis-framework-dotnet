using System.Reflection;
using System.Runtime.Loader;

namespace Scaffolds.ECommerce.ArchitectureTests;

internal static class HostAssembly
{
    private const string Name = "Scaffolds.ECommerce.Host";
    private const string HostSolutionFolder = "Host";

    private static readonly Lazy<Assembly> Loaded = new(LoadByName);

    public static Assembly Value => Loaded.Value;

    private static Assembly LoadByName()
    {
        var hostOutput = HostOutputDirectory();
        AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
        {
            var candidate = Path.Combine(hostOutput, $"{assemblyName.Name}.dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };

        return AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(Name));
    }

    private static string HostOutputDirectory()
    {
        var testProjectName = typeof(HostAssembly).Assembly.GetName().Name;
        var testProject = new DirectoryInfo(AppContext.BaseDirectory);
        while (testProject is not null && testProject.Name != testProjectName)
            testProject = testProject.Parent;

        var solution = testProject?.Parent?.Parent
            ?? throw new InvalidOperationException($"'{AppContext.BaseDirectory}' is not the build output of {testProjectName}.");
        var buildOutput = Path.GetRelativePath(testProject.FullName, AppContext.BaseDirectory);
        var hostOutput = Path.Combine(solution.FullName, HostSolutionFolder, Name, buildOutput);

        return File.Exists(Path.Combine(hostOutput, $"{Name}.dll"))
            ? hostOutput
            : throw new InvalidOperationException($"{Name} is not built in '{hostOutput}'; build the solution before the architecture tests.");
    }
}
