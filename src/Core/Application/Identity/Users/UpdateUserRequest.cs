namespace FSH.WebApi.Application.Identity.Users;

public class UpdateUserRequest : IRequest<string>
{
    public string? UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool? Gender { get; set; }
    public DateOnly? BirthDate { get; set; }
}

public class UpdateUserRequestValidator : CustomValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator(IStringLocalizer<UpdateUserRequestValidator> T)
    {
        RuleFor(p => p.FirstName)
           .MaximumLength(75);

        RuleFor(p => p.LastName)
            .MaximumLength(75);

        RuleFor(p => p.BirthDate)
            .LessThan(DateOnly.FromDateTime(DateTime.Today))
                .WithMessage(T["Birth date must be in the past."]);
    }
}

public class UpdateUserRequestHandler : IRequestHandler<UpdateUserRequest, string>
{
    private readonly IUserService _userService;
    private readonly IStringLocalizer<UpdateUserRequestHandler> _t;

    public UpdateUserRequestHandler(IUserService userService, IStringLocalizer<UpdateUserRequestHandler> t)
    {
        _userService = userService;
        _t = t;
    }

    public async Task<string> Handle(UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.GetAsync(request.UserId!, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException(_t["User not found."]);
        }

        await _userService.UpdateAsync(request);
        return _t["Profile updated successfully."];
    }
}