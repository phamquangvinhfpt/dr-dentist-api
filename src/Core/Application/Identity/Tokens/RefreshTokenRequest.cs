namespace FSH.WebApi.Application.Identity.Tokens;

public record RefreshTokenRequest(string Token, string RefreshToken);

public class RefreshTokenRequestValidator : CustomValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(p => p.Token).Cascade(CascadeMode.Stop)
            .NotEmpty();

        RuleFor(p => p.RefreshToken).Cascade(CascadeMode.Stop)
            .NotEmpty();
    }
}
