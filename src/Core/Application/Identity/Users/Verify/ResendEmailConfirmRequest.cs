using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users.Verify;
public class ResendEmailConfirmRequest : IRequest<string>
{
    public string Origin { get; set; }
    public string Local {  get; set; }
}

public class ResendEmailConfirmRequestHandler : IRequestHandler<ResendEmailConfirmRequest, string>
{
    private readonly IUserService _userService;
    private readonly ICurrentUser _currentUser;

    public ResendEmailConfirmRequestHandler(IUserService userService, ICurrentUser currentUser)
    {
        _userService = userService;
        _currentUser = currentUser;
    }

    public async Task<string> Handle(ResendEmailConfirmRequest request, CancellationToken cancellationToken)
    {
        return await _userService.ResendEmailCodeConfirm(_currentUser.GetUserId().ToString(), request.Local, request.Origin);
    }
}
