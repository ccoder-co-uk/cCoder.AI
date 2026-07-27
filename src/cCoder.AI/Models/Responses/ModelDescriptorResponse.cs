// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Models.Responses;

public class ModelDescriptorResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? Publisher { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
}