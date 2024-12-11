using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.CustomerServices.Feedbacks;
public class FeedbackServiceDetail
{
    public Guid FeedbackId { get; set; }
    public string DoctorID { get; set; }
    public string? DoctorName { get; set; }
    public string? PatientID { get; set; }
    public string? PatientName { get; set; }
    public DateTime CreateDate { get; set; }
    public int Ratings { get; set; }
    public string? Message { get; set; }
    public bool CanFeedback { get; set; }
}
