using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users;
public class UpdatePatientFamilyRequest
{
    public string? PatientId { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public PatientFamilyRelationship Relationship { get; set; }
}
