using DocumentFormat.OpenXml.Spreadsheet;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Mailing;
using FSH.WebApi.Application.Identity.Users.Password;
using FSH.WebApi.Application.Identity.Users.Profile;
using Microsoft.AspNetCore.WebUtilities;

namespace FSH.WebApi.Infrastructure.Identity;

internal partial class UserService
{
    public async Task<string> ForgotPasswordAsync(ForgotPasswordRequest request, string local, string origin)
    {
        EnsureValidTenant();

        var user = await _userManager.FindByEmailAsync(request.Email.Normalize());
        if (user is null || !await _userManager.IsEmailConfirmedAsync(user))
        {
            // Don't reveal that the user does not exist or is not confirmed
            throw new InternalServerException(_t["An Error has occurred!"]);
        }

        // For more information on how to enable account confirmation and password reset please
        // visit https://go.microsoft.com/fwlink/?LinkID=532713
        string code = await _userManager.GeneratePasswordResetTokenAsync(user);
        const string route = "auth/reset-password";
        var endpointUri = new Uri(string.Concat($"{origin}/", route));
        string passwordResetUrl = QueryHelpers.AddQueryString(endpointUri.ToString(), "Token", code);
        passwordResetUrl = QueryHelpers.AddQueryString(passwordResetUrl, "Email", request.Email);
        RegisterUserEmailModel eMailModel = new RegisterUserEmailModel()
        {
            Email = user.Email,
            UserName = user.UserName,
            Url = passwordResetUrl
        };
        if (local.Equals("en"))
        {
            var mailRequest = new MailRequest(
            new List<string> { request.Email },
            _t["Reset Password"],
            _templateService.GenerateEmailTemplate("reset-password-en", eMailModel));
            _jobService.Enqueue(() => _mailService.SendAsync(mailRequest, CancellationToken.None));
        }
        else
        {
            var mailRequest = new MailRequest(
            new List<string> { request.Email },
            _t["Reset Password"],
            _templateService.GenerateEmailTemplate("reset-password-vie", eMailModel));
            _jobService.Enqueue(() => _mailService.SendAsync(mailRequest, CancellationToken.None));
        }
        return _t["Password Reset Mail has been sent to your authorized Email."];
    }

    public async Task<string> ResetPasswordAsync(ResetPasswordRequest request)
    {
        try
        {
            var res = await _reCAPTCHAv3Service.Verify(request.captchaToken);
            if (!res.success)
            {
                throw new UnauthorizedException(_t["reCAPTCHA Verification Failed."]);
            }
        }
        catch (Exception ex)
        {
            throw new UnauthorizedException(ex.Message);
        }

        var user = await _userManager.FindByEmailAsync(request.Email?.Normalize()!);

        // Don't reveal that the user does not exist
        _ = user ?? throw new InternalServerException(_t["An Error has occurred!"]);

        var result = await _userManager.ResetPasswordAsync(user, request.Token!, request.Password!);

        return result.Succeeded
            ? _t["Password Reset Successful!"]
            : throw new InternalServerException(_t["An Error has occurred!"]);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest model)
    {
        if (!_currentUserService.GetUserId().ToString().Equals(model.UserID))
        {
            throw new BadRequestException("User ID is not available.");
        }
        var user = await _userManager.FindByIdAsync(model.UserID) ?? throw new NotFoundException(_t["User Not Found."]);

        var result = await _userManager.ChangePasswordAsync(user, model.Password, model.NewPassword);

        if (!result.Succeeded)
        {
            throw new InternalServerException(_t["Change password failed"], result.GetErrors(_t));
        }
    }

    public async Task<bool> VerifyCurrentPassword(string userId, string password)
    {
        var user = await _userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException(_t["User Not Found."]);

        return await _userManager.CheckPasswordAsync(user, password);
    }
}