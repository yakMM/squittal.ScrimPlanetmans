using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace squittal.ScrimPlanetmans.App.Services
{
    public class IpCheckFilter : ActionFilterAttribute
    {
        private readonly IConfiguration _conf;
        private readonly IHttpContextAccessor _context;

        public string remoteIp { get; }

        public IpCheckFilter(IHttpContextAccessor context, IConfiguration configuration)
        {
            _conf = configuration;
            _context = context;
            remoteIp = _context.HttpContext.Connection.RemoteIpAddress.ToString();
        }

        public bool IsIpAdmin()
        {
            var remoteIp = _context.HttpContext.Connection.RemoteIpAddress;
            var ipList = _conf.GetValue<string>("AdminIpAddresses").Split(';');


            if (remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            foreach (var address in ipList)
            {
                var testIp = IPAddress.Parse(address);

                if (testIp.Equals(remoteIp))
                {
                    return true;
                }
            }
            return false;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if(IsIpAdmin())
            {
                base.OnActionExecuting(context);
            }
            else
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }            
        }
    }

    public class IpAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IpCheckFilter _filter;
        public IpAuthStateProvider(IpCheckFilter filter)
        {
            _filter = filter;
        }
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            ClaimsIdentity identity;
            if(_filter.IsIpAdmin())
            {
                identity = new ClaimsIdentity(new[]{new Claim(ClaimTypes.Name, _filter.remoteIp) }, "Ip Authentification");
            }
            else
            {
                identity = new ClaimsIdentity();
            }
            
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }
}
