using FSH.WebApi.Application.CustomerServices.Feedbacks;
using FSH.WebApi.Application.DentalServices.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices;
public class ServiceHaveFeedback
{
    public ServiceDTO? ServiceDTO { get; set; }
    public List<FeedbackServiceResponse>? Feedbacks { get; set; }
}
