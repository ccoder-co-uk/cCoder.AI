// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace cCoder.AI.Dependencies;

internal sealed class LegacyRouteApiDescriptionDependency
    : IApiDescriptionProvider
{
    public int Order => int.MaxValue;

    public void OnProvidersExecuted(
        ApiDescriptionProviderContext context)
    {
        for (int index = context.Results.Count - 1;
            index >= 0;
            index--)
        {
            ApiDescription description = context.Results[index];

            if (description.RelativePath?.StartsWith(
                    value: "Api/Model/",
                    comparisonType:
                        StringComparison.OrdinalIgnoreCase) == true)
            {
                context.Results.RemoveAt(index: index);
            }
        }
    }

    public void OnProvidersExecuting(
        ApiDescriptionProviderContext context)
    { }
}
