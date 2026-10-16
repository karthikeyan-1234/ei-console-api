using System;
using System.Collections.Generic;
using System.Text;

namespace _04_EI_Console.Shared.Application.Contracts
{
    /// <summary>
    /// One key-value pair in a form-data or url-encoded request body.
    ///
    /// ValueType = "Text" — Value is JSONata, evaluated against the current
    ///                       pipeline item. Result stringified.
    /// ValueType = "File" — Value is a URL, or JSONata yielding a URL. The
    ///                       executor downloads it at send time and adds it as
    ///                       a file part.
    /// </summary>
    public sealed record FormFieldDto(
        string Key,
        string ValueType,
        string Value,
        string? FileName,
        string? ContentType);
}
