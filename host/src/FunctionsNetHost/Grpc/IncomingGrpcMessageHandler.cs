﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Grpc.Messages;

namespace FunctionsNetHost.Grpc
{
    internal sealed class IncomingGrpcMessageHandler
    {
        private bool _specializationDone;
        private readonly AppLoader _appLoader;

        internal IncomingGrpcMessageHandler(AppLoader appLoader)
        {
            _appLoader = appLoader;
        }

        internal Task ProcessMessageAsync(StreamingMessage message)
        {
            Task.Run(() => Process(message));

            return Task.CompletedTask;
        }

        private async Task Process(StreamingMessage msg)
        {
            if (_specializationDone)
            {
                Logger.LogDebug("Specialization done. So forward all messages to customer payload");

                // Specialization done. So forward all messages to customer payload.
                await MessageChannel.Instance.SendInboundAsync(msg);
                return;
            }

            var responseMessage = new StreamingMessage();

            switch (msg.ContentCase)
            {
                case StreamingMessage.ContentOneofCase.WorkerInitRequest:
                    {
                        responseMessage.WorkerInitResponse = BuildWorkerInitResponse();
                        break;
                    }
                case StreamingMessage.ContentOneofCase.FunctionsMetadataRequest:
                    {
                        responseMessage.FunctionMetadataResponse = BuildFunctionMetadataResponse();
                        break;
                    }
                case StreamingMessage.ContentOneofCase.FunctionEnvironmentReloadRequest:

                    Logger.LogDebug("Specialization request received");

                    var envReloadRequest = msg.FunctionEnvironmentReloadRequest;
                    foreach (var kv in envReloadRequest.EnvironmentVariables)
                    {
                        Logger.LogDebug($"{kv.Key}:{kv.Value}");
                        Environment.SetEnvironmentVariable(kv.Key, kv.Value);
                    }
                    Logger.LogDebug($"Set {envReloadRequest.EnvironmentVariables.Count} environment variables.");

                    var applicationExePath = PathUtils.GetApplicationExePath(envReloadRequest.FunctionAppDirectory);
                    Logger.LogDebug($"applicationExePath {applicationExePath}");

#pragma warning disable CS4014
                    Task.Run(() =>
#pragma warning restore CS4014
                    {
                        Logger.LogDebug($"About to call RunApplication in a new Task/Thread");

                        _ = _appLoader.RunApplication(applicationExePath);
                    });

                    Logger.LogDebug($"Will wait for worker loaded signal");
                    WorkerLoadStatusSignalManager.Instance.Signal.WaitOne();
                    Logger.LogDebug($"Received worker loaded signal. Forwarding environment reload request to worker.");

                    await MessageChannel.Instance.SendInboundAsync(msg);
                    _specializationDone = true;
                    break;
            }


            await MessageChannel.Instance.SendOutboundAsync(responseMessage);
        }

        private static FunctionMetadataResponse BuildFunctionMetadataResponse()
        {
            var metadataResponse = new FunctionMetadataResponse
            {
                UseDefaultMetadataIndexing = true,
                Result = new StatusResult { Status = StatusResult.Types.Status.Success }
            };

            return metadataResponse;
        }

        private static WorkerInitResponse BuildWorkerInitResponse()
        {
            var response = new WorkerInitResponse
            {
                Result = new StatusResult { Status = StatusResult.Types.Status.Success }
            };

            return response;
        }
    }
}
