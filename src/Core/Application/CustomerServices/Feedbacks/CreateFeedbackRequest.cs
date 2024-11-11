using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.CustomerServices.Feedbacks;
public class CreateFeedbackRequest : IRequest<string>
{
    public Guid? PatientProfileId { get; set; }
    public Guid? DoctorProfileId { get; set; }
    public Guid ServiceId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Rating { get; set; }
}
