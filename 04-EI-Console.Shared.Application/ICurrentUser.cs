using System;
using System.Collections.Generic;
using System.Text;

namespace _04_EI_Console.Shared.Application
{
    public interface ICurrentUser
    {
        int UserId { get; }
        string Subject { get; }
        bool IsAuthenticated { get; }
    }
}
