// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;

namespace cCoder.AI.Brokers.Loggings;

public interface ILoggingBroker
{
    void LogError(Exception exception, string message, params object[] args);
}