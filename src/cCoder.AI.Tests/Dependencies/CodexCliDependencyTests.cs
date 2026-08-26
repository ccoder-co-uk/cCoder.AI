// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.Diagnostics;
using System.Reflection;
using cCoder.AI.Dependencies;
using FluentAssertions;

namespace cCoder.AI.Tests.Units;

public sealed partial class CodexCliDependencyTests
{
    [Fact]
    public void ShouldPlaceEveryImageAfterItsImageArgument()
    {
        // Given
        string firstPath = Path.GetTempFileName() + ".png";
        string secondPath = Path.GetTempFileName() + ".jpg";
        File.WriteAllBytes(firstPath, [1]);
        File.WriteAllBytes(secondPath, [2]);
        ProcessStartInfo startInfo = new();
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("-");
        MethodInfo method = typeof(CodexCliDependency).GetMethod(
            "AddInputFiles",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        // When
        method.Invoke(null, [startInfo, new[] { firstPath, secondPath }]);

        // Then
        startInfo.ArgumentList.Should().Equal(
            "exec",
            "--image",
            Path.GetFullPath(firstPath),
            "--image",
            Path.GetFullPath(secondPath),
            "-");
    }
}
