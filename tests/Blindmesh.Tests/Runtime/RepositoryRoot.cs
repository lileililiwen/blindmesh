namespace Blindmesh.Tests.Runtime;

/// <summary>Finds the repository root from the test output directory.</summary>
public static class RepositoryRoot
{
    public static DirectoryInfo Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Blindmesh.sln")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
