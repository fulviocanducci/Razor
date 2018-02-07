// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Language
{
    internal class EmptyProject : RazorProject
    {
        public static readonly EmptyProject Instance = new EmptyProject();

        private EmptyProject()
        {
        }

        public override IEnumerable<RazorProjectItem> EnumerateItems(string basePath)
        {
            return Array.Empty<RazorProjectItem>();
        }

        public override RazorProjectItem GetItem(string path)
        {
            return null;
        }
    }
}
