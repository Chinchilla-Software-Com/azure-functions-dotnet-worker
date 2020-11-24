﻿using System;
using System.Threading.Channels;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.Functions.Worker.Logging
{
    public class InvocationLogger : ILogger
    {
        private string _invocationId;
        private ChannelWriter<StreamingMessage> _channelWriter;

        public InvocationLogger(string invocationId, ChannelWriter<StreamingMessage> channelWriter)
        {
            _invocationId = invocationId;
            _channelWriter = channelWriter;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var response = new StreamingMessage();
            string message = formatter(state, exception);
            response.RpcLog = new RpcLog() { InvocationId = _invocationId, EventId = eventId.ToString(), Message = message };
            _channelWriter.TryWrite(response);
        }
    }
}
