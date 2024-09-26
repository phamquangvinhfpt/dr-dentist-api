namespace FSH.WebApi.Application.Identity.Tokens;

public record TokenRequest(string Email, string Password, string CaptchaToken, string? DeviceId);

public class TokenRequestValidator : CustomValidator<TokenRequest>
{
    public TokenRequestValidator()
    {
        RuleFor(p => p.Email).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .EmailAddress()
                .WithMessage("Invalid Email Address.");

        RuleFor(p => p.Password).Cascade(CascadeMode.Stop)
            .NotEmpty();

        RuleFor(p => p.CaptchaToken).Cascade(CascadeMode.Stop)
            .NotEmpty();
    }
}