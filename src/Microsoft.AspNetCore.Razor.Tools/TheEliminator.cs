using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Tools
{
    internal class TheEliminator : RazorEnginePhaseBase
    {
        protected override void ExecuteCore(RazorCodeDocument codeDocument)
        {
            var irDocument = codeDocument.GetDocumentIntermediateNode();
            var method = irDocument.FindPrimaryMethod();
            method.Children.Clear();
        }
    }
}
