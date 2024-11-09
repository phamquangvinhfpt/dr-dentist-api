using FSH.WebApi.Domain.CustomerServices;

namespace FSH.WebApi.Application.Identity.Users;

public class FeedBackDoctorResponse
{
    public int RatingType { get; set; }
    public List<FeedBackResponse>? Feedbacks { get; set; }
}