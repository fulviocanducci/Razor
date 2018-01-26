// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis.CommandLine;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class RazorGeneratePass1 : DotNetToolTask
    {
        [Required]
        public string[] Sources { get; set; }

        [Required]
        public string[] OutputFiles { get; set; }

        [Required]
        public string ProjectRoot { get; set; }

        internal override string Command => "generate-pass1";

        protected override string GenerateResponseFileCommands()
        {
            var builder = new StringBuilder();

            builder.AppendLine(Command);

            builder.AppendLine("-p");
            builder.AppendLine(ProjectRoot);

            for (var i = 0; i < Sources.Length; i++)
            {
                builder.AppendLine("-s");
                builder.AppendLine(Sources[i]);
            }

            for (var i = 0; i < Sources.Length; i++)
            {
                builder.AppendLine("-o");
                builder.AppendLine(OutputFiles[i]);
            }

            return builder.ToString();
        }
    }
}
