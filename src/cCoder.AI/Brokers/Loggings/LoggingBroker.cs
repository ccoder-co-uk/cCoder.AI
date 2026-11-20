// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System;
using Microsoft.Extensions.Logging;

namespace cCoder.AI.Brokers.Loggings;

internal sealed class LoggingBroker(ILogger<LoggingBroker> logger) : ILoggingBroker
{
    public void LogInformation(string message, params object[] args) =>
        logger.LogInformation(message: message, args: args);

    public void LogWarning(Exception exception, string message, params object[] args) =>
        logger.LogWarning(exception: exception, message: message, args: args);

    public void LogError(Exception exception, string message, params object[] args) =>
        logger.LogError(exception: exception, message: message, args: args);
}
