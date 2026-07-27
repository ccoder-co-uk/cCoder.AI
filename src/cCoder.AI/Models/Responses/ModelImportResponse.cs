// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Models.Responses;

public class ModelImportResponse
{
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string RawContent { get; set; } = string.Empty;
}