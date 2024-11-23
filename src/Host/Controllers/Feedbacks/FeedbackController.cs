using FSH.WebApi.Application.CustomerServices.Feedbacks;
using FSH.WebApi.Application.TreatmentPlan;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FSH.WebApi.Host.Controllers.Feedbacks;
public class FeedbackController : VersionedApiController
{
    private readonly IFeedbackService _feedbackService;

    public FeedbackController(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    [HttpPost("create")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Create Feedback with appointment", "")]
    public Task<string> UpdateTreatmentPlanDetail(CreateFeedbackRequest request, CancellationToken cancellationToken)
    {
        return _feedbackService.CreateFeedback(request, cancellationToken);
    }
    [HttpPost("update")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Update Feedback with appointment", "")]
    public Task<string> UpdateFeedback(CreateFeedbackRequest request, CancellationToken cancellationToken)
    {
        return _feedbackService.UpdateFeedback(request, cancellationToken);
    }

    [HttpDelete("delete/{id}")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Delete Feedback", "")]
    public Task<string> DeleteFeedback(Guid id, CancellationToken cancellationToken)
    {
        return _feedbackService.DeleteFeedback(id, cancellationToken);
    }
}
