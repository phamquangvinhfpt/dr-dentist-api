using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users.Profile;
public class MedicalHistoryRequest
{
    public string[] MedicalName { get; set; } = Array.Empty<string>();
    public string? Note { get; set; }
}
public class MedicalHistoryRequestValidator : CustomValidator<MedicalHistoryRequest>
{
    public MedicalHistoryRequestValidator()
    {

        RuleFor(x => x.MedicalName)
            .NotEmpty()
            .WithMessage("At least one medical name is required");
    }
}
