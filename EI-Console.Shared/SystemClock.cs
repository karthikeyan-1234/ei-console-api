using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Shared
{
    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
