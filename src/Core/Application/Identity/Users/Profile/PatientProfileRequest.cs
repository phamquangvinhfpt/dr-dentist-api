using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users.Profile;
public class PatientProfileRequest
{
    public string? UserId { get; set; }
    public string? IDCardNumber { get; set; }
    public string? Occupation { get; set; }
}
public class PatientProfileRequestValidator : CustomValidator<PatientProfileRequest>
{
    public PatientProfileRequestValidator(IUserService userService)
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .MustAsync(async (id, _) => await userService.ExistsWithUserIDAsync(id))
                .WithMessage((_, id) => $"User {id} is not existed.");
    }
}
