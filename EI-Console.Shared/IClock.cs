using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Shared
{
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }
}
