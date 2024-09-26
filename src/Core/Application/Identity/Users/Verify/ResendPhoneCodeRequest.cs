namespace FSH.WebApi.Application.Identity.Users.Verify;
public class ResendPhoneCodeRequest : IRequest<string>
{
}

public class ResendPhoneCodeRequestHandler : IRequestHandler<ResendPhoneCodeRequest, string>
{
    private readonly IUserService _userService;
    private readonly ICurrentUser _currentUser;

    public ResendPhoneCodeRequestHandler(
               IUserService userService,
               ICurrentUser currentUser)
        => (_userService, _currentUser)
            = (userService, currentUser);

    public async Task<string> Handle(ResendPhoneCodeRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        return await _userService.ResendPhoneNumberCodeConfirm(userId.ToString());
    }
}
