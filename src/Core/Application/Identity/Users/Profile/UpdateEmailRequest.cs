namespace FSH.WebApi.Application.Identity.Users.Profile;
public class UpdateEmailRequest : IRequest<string>
{
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Origin { get; set; }
}

public class UpdateEmailRequestValidator : CustomValidator<UpdateEmailRequest>
{
    public UpdateEmailRequestValidator(IUserService userService, IStringLocalizer<UpdateUserRequestValidator> T)
    {

        RuleFor(p => p.Email)
            .NotEmpty()
            .EmailAddress()
                .WithMessage(T["Invalid Email Address."])
            .MustAsync(async (user, email, _) => !await userService.ExistsWithEmailAsync(email, user.UserId))
                .WithMessage((_, email) => string.Format(T["Email {0} is already registered."], email));

        RuleFor(x => x.Password)
            .NotEmpty()
            .MustAsync(async (user, password, _) => await userService.VerifyCurrentPassword(user.UserId, password))
                .WithMessage(T["Invalid password."]);
    }
}

public class UpdateEmailRequestHandler : IRequestHandler<UpdateEmailRequest, string>
{
    private readonly IUserService _userService;
    private readonly IStringLocalizer<UpdateEmailRequestHandler> _t;

    public UpdateEmailRequestHandler(IUserService userService, IStringLocalizer<UpdateEmailRequestHandler> t)
    {
        _userService = userService;
        _t = t;
    }

    public async Task<string> Handle(UpdateEmailRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.GetAsync(request.UserId!, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException(_t["User not found."]);
        }

        return await _userService.UpdateEmailAsync(request);
    }
}