// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using cCoder.AI.Brokers.Loggings;

namespace AI.AcceptanceTests.Infrastructure;

public sealed class RecordingLoggingBroker : ILoggingBroker
{
    public List<Exception> Exceptions { get; } = [];

    public void Reset() =>
        Exceptions.Clear();

    public void LogError(
        Exception exception,
        string message,
        params object[] args) =>
        Exceptions.Add(item: exception);
}
