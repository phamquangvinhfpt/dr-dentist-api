namespace FSH.WebApi.Application.Common.ReCaptchaV3;
public interface IReCAPTCHAv3Service : ITransientService
{
    Task<ReCAPTCHAv3Response> Verify(string token);
}
