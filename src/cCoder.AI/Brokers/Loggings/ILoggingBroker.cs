// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.AI.Brokers.Loggings;

public interface ILoggingBroker
{
    void LogError(Exception exception, string message, params object[] args);
}