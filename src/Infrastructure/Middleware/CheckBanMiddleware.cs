using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Middleware;

public class CheckBanMiddleware : IMiddleware
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;

    public CheckBanMiddleware(UserManager<ApplicationUser> userManager, ICurrentUser currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var id = _currentUser.GetUserId();
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (await _userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedAccessException("Unauthorization: You was ban");
        }
        else
        {
            await next(context);
        }
    }
}
