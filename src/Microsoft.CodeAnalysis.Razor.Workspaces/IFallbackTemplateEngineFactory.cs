// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Razor
{
    // Used to create the 'fallback' template engine when we don't have a custom implementation.
    internal interface IFallbackTemplateEngineFactory : ITemplateEngineFactory
    {
    }
}
