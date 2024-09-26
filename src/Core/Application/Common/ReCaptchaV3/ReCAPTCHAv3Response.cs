namespace FSH.WebApi.Application.Common.ReCaptchaV3;

public class ReCAPTCHAv3Response
{
    public bool success { get; set; }
    public string challengeTs { get; set; } = string.Empty;
    public string hostname { get; set; } = string.Empty;
    public double score { get; set; }
    public string action { get; set; } = string.Empty;
}