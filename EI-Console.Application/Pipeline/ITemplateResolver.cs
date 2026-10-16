using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Application.Pipeline
{
    public interface ITemplateResolver
    {
        string Resolve(string? template, PipelineContext context);
    }
}
