// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.IO;
using Xunit;
using cCoder.AI.Exposures;
using FluentAssertions;

namespace cCoder.AI.Tests;

public sealed partial class RuntimePackagingTests
{
    [Fact]
    public void AIRuntime_WhenOutputIsBuilt_ExcludesAnalyzerAssemblies()
    {
        // Given
        string runtimeOutputDirectory = Path.GetDirectoryName(
            path: typeof(IModelManager).Assembly.Location);

        string analyzerAssemblyPath = Path.Combine(
            path1: runtimeOutputDirectory,
            path2: "cCoder.CodeAnalysis.dll");

        string contractsAssemblyPath = Path.Combine(
            path1: runtimeOutputDirectory,
            path2: "cCoder.CodeAnalysis.Contracts.dll");

        // When
        bool analyzerAssemblyExists = File.Exists(path: analyzerAssemblyPath);
        bool contractsAssemblyExists = File.Exists(path: contractsAssemblyPath);

        // Then
        analyzerAssemblyExists.Should()
            .BeFalse();

        contractsAssemblyExists.Should()
            .BeFalse();
    }
}