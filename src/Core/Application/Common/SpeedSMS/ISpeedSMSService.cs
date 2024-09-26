namespace FSH.WebApi.Application.Common.SpeedSMS;
public interface ISpeedSMSService : ITransientService
{
    public String sendSMS(String[] phones, String content, int type);
}
