using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.CustomerServices.Feedbacks;
public class CreateFeedbackRequest : IRequest<string>
{
    //public Guid? PatientProfileId { get; set; }
    //public Guid? DoctorProfileId { get; set; }
    //public Guid ServiceId { get; set; }
    public Guid FeedbackID { get; set; }
    public Guid AppointmentID { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Rating { get; set; }
}

public class CreateFeedbackRequestValidator : CustomValidator<CreateFeedbackRequest>
{
    public CreateFeedbackRequestValidator()
    {
        //RuleFor(p => p.PatientProfileId)
        //    .NotNull()
        //    .WithMessage("The patient information should be include");

        //RuleFor(p => p.DoctorProfileId)
        //    .NotNull()
        //    .WithMessage("The doctor information should be include");

        //RuleFor(p => p.ServiceId)
        //    .NotNull()
        //    .WithMessage("The service information should be include");

        RuleFor(p => p.FeedbackID)
            .NotNull()
            .When(p => p.AppointmentID == default)
            .WithMessage("The Feedback information should be include");

        RuleFor(p => p.AppointmentID)
            .NotNull()
            .When(p => p.FeedbackID == default)
            .WithMessage("The Appointment information should be include");

        When(p => !string.IsNullOrEmpty(p.Message), () =>
        {
            RuleFor(p => p.Message)
                .MaximumLength(1000)
                .WithMessage("Message cannot exceed 1000 characters");
        });

        When(p => p.Rating != default, () =>
        {
            RuleFor(p => p.Rating)
                .Must(p => p >= 0 && p <= 5)
                .WithMessage("Rating max in 5");
        });
    }
}
