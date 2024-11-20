using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.CustomerServices.Feedbacks;
public class FeedbackServiceResponse
{
    public int RatingType { get; set; }
    public List<FeedbackServiceDetail>? Feedbacks { get; set; }
}
