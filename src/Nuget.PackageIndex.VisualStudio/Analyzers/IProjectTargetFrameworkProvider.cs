﻿using System.Collections.Generic;

namespace Nuget.PackageIndex.VisualStudio.Analyzers
{
    /// <summary>
    /// Projects should Export this interface to provide target frameworks supported 
    /// by given DTE project. This is needed since different rpojec system types might have
    /// different strategy for target frameworks. For example csproj has one target framework, 
    /// xproj can have multiple.
    /// </summary>
    public interface IProjectTargetFrameworkProvider
    {
        bool SupportsProject(EnvDTE.Project project);
        IEnumerable<string> GetTargetFrameworks(EnvDTE.Project project);
    }
}