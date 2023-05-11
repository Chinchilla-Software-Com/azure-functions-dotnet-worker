﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Globalization;

namespace FunctionsNetHost
{
    internal static class Logger
    {
        const string logPrefix = ""; //"LanguageWorkerConsoleLog";

        internal static void Log(string message)
        {
            string ts = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            Console.WriteLine($"{logPrefix}[{ts}] [FunctionsNetHost] {message}");
        }
    }
}