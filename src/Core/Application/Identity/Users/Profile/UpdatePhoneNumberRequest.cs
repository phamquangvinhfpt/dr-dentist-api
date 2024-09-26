namespace FSH.WebApi.Application.Identity.Users.Profile;
public class UpdatePhoneNumberRequest : IRequest<string>
{
    public string? UserId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Password { get; set; }
}

public class UpdatePhoneNumberRequestValidator : CustomValidator<UpdatePhoneNumberRequest>
{
    public UpdatePhoneNumberRequestValidator(IUserService userService, IStringLocalizer<UpdatePhoneNumberRequestValidator> T)
    {

        RuleFor(u => u.PhoneNumber).Cascade(CascadeMode.Stop)
            .MustAsync(async (user, phone, _) => !await userService.ExistsWithPhoneNumberAsync(phone!, user.UserId))
                .WithMessage((_, phone) => string.Format(T["Phone number {0} is already registered."], phone))
                .Unless(u => string.IsNullOrWhiteSpace(u.PhoneNumber));

        RuleFor(x => x.Password)
            .NotEmpty()
            .MustAsync(async (user, password, _) => await userService.VerifyCurrentPassword(user.UserId, password))
                .WithMessage(T["Invalid password."]);
    }
}

public class UpdatePhoneNumberRequestHandler : IRequestHandler<UpdatePhoneNumberRequest, string>
{
    private readonly IUserService _userService;
    private readonly IStringLocalizer<UpdatePhoneNumberRequestHandler> _t;

    public UpdatePhoneNumberRequestHandler(IUserService userService, IStringLocalizer<UpdatePhoneNumberRequestHandler> t)
    {
        _userService = userService;
        _t = t;
    }

    public async Task<string> Handle(UpdatePhoneNumberRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.GetAsync(request.UserId!, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException(_t["User not found."]);
        }

        await _userService.UpdatePhoneNumberAsync(request);
        return _t["Phone number updated successfully."];
    }
}
