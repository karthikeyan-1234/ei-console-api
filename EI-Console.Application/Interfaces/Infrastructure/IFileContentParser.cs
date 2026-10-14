using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace EI_Console.Application.Interfaces.Infrastructure
{
    /// <summary>
    /// Parses the raw content of a downloaded file into a JSON value that feeds
    /// the pipeline context. Handles JSON, CSV, and plain text.
    ///
    /// The format hint may be "Auto", "Json", "Csv", or "Text". Auto detects
    /// from file extension and content shape.
    /// </summary>
    public interface IFileContentParser
    {
        JsonElement Parse(string content, string formatHint, string remotePath);
    }
}
