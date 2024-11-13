using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users.Profile;
public class PatientFamilyRequest
{
    
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public PatientFamilyRelationship Relationship { get; set; }
}
public class PatientFamilyRequestValidator : CustomValidator<PatientFamilyRequest>
{
    public PatientFamilyRequestValidator()
    {

        RuleFor(x => x.Name)
            .NotEmpty();

        RuleFor(x => x.Phone)
            .NotEmpty();

        RuleFor(x => x.Email)
            .EmailAddress().When(x => x.Email != null);

        RuleFor(x => x.Relationship)
            .IsInEnum();
    }
}