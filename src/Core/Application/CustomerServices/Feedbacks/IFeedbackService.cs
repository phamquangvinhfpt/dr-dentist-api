using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.CustomerServices.Feedbacks;
public interface IFeedbackService : ITransientService
{
    Task<string> CreateFeedback(CreateFeedbackRequest request, CancellationToken cancellationToken);
    Task<string> UpdateFeedback(CreateFeedbackRequest request, CancellationToken cancellationToken);
    Task<string> DeleteFeedback(Guid id, CancellationToken cancellationToken);
}
