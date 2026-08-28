// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Configurations;

namespace AI.Web.Models;

public sealed class AppConfiguration
{
    public AIConfiguration AI { get; set; } = new();
}