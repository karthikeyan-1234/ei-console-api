using _04_EI_Console.Shared.Application;

using Microsoft.AspNetCore.Http;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace EI_Console.Infrastructure.Identity
{
    public sealed class CurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _accessor;

        public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

        public int UserId => TryGetIntClaim("user_id") ?? 1;
        public string Subject => _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "dev-user";
        public bool IsAuthenticated => _accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

        private int? TryGetIntClaim(string type)
        {
            var raw = _accessor.HttpContext?.User.FindFirstValue(type);
            return int.TryParse(raw, out var v) ? v : null;
        }
    }
}
