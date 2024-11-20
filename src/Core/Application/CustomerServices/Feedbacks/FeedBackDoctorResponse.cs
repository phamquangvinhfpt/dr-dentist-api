using FSH.WebApi.Domain.CustomerServices;

namespace FSH.WebApi.Application.CustomerServices.Feedbacks;

public class FeedBackDoctorResponse
{
    public int RatingType { get; set; }
    public int TotalRating { get; set; }
    public List<FeedBackResponse>? Feedbacks { get; set; }
}