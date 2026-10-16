using Microsoft.AspNetCore.SignalR;

using System;
using System.Collections.Generic;
using System.Text;

namespace EI_Console.Infrastructure.Pipeline
{

    /// <summary>
    /// SignalR hub for real-time execution events. Currently a broadcast channel —
    /// every connected client receives every event. Once auth and multi-tenancy
    /// land, connections will be grouped by tenant and events routed to the
    /// tenant's group only.
    /// </summary>
    public class EiHub: Hub
    {
    }
}
