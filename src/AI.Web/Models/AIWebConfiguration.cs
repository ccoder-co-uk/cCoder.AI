// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Configurations;

namespace AI.Web.Models;

public sealed class AIWebConfiguration
{
    public AIWebConfiguration() =>
        AI = new AIConfiguration();

    public AIConfiguration AI { get; set; }
}