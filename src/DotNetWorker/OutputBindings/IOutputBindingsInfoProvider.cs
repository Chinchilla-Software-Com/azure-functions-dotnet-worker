﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Functions.Worker.OutputBindings
{
    internal interface IOutputBindingsInfoProvider
    {
        public OutputBindingsInfo GetBindingsInfo(FunctionMetadata metadata);
    }
}
