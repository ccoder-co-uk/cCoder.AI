// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using cCoder.AI.Brokers.Loggings;

namespace AI.AcceptanceTests.Infrastructure;

public sealed class RecordingLoggingBroker : ILoggingBroker, AI.Web.Brokers.Loggings.ILoggingBroker
{
    public List<Exception> Exceptions { get; } = [];
    public int InformationCount { get; private set; }
    public int WarningCount { get; private set; }
    public int ErrorCount { get; private set; }

    public void Reset()
    {
        Exceptions.Clear();
        InformationCount = 0;
        WarningCount = 0;
        ErrorCount = 0;
    }

    public void LogInformation(string message, params object[] args) =>
        InformationCount++;

    public void LogWarning(Exception exception, string message, params object[] args)
    {
        WarningCount++;
        Exceptions.Add(item: exception);
    }

    public void LogError(
        Exception exception,
        string message,
        params object[] args)
    {
        ErrorCount++;
        Exceptions.Add(item: exception);
    }
}
