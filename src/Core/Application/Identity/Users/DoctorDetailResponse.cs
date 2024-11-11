using FSH.WebApi.Application.CustomerServices.Feedbacks;
using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users;
public class DoctorDetailResponse
{
    public string Id { get; set; }
    public string? UserName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool? Gender { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ImageUrl { get; set; }
    public double? Rating { get; set; }
    public int? TotalFeedback { get; set; }
    public DoctorProfile? DoctorProfile { get; set; }
    public List<FeedBackDoctorResponse>? DoctorFeedback { get; set; }
}
