using System.Collections.Generic;

namespace XelaBuild.Core;

public abstract class ProjectsProvider
{
    public abstract IEnumerable<string> GetProjectPaths();

    public abstract string BuildFolder { get; }

    public static ProjectsProvider FromList(IEnumerable<string> projectPaths, string buildFolder)
    {
        return new ProjectsProviderFromList(projectPaths, buildFolder);
    }

    private class ProjectsProviderFromList : ProjectsProvider
    {
        private readonly List<string> _projectPaths;

        public ProjectsProviderFromList(IEnumerable<string> projectPaths, string buildFolder)
        {
            _projectPaths = new List<string>(projectPaths);
            BuildFolder = buildFolder;
        }

        public override IEnumerable<string> GetProjectPaths()
        {
            return _projectPaths;
        }

        public override string BuildFolder { get; }
    }
}