using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
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
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CheckBanMiddleware(ApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var id = _currentUser.GetUserId();
        if(id != default)
        {
            var user = _db.Users.FirstOrDefault(p => p.Id == id.ToString());
            if (user == null)
            {
                throw new UnauthorizedAccessException("Unauthorization: You was ban");
            }
        }
        await next(context);
    }
}
