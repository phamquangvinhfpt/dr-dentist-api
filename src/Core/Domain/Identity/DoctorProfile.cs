using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Domain.Identity;
public class DoctorProfile : AuditableEntity, IAggregateRoot
{
    public string? DoctorId { get; set; }
    public string? Education { get; set; }
    public string? College { get; set; }
    public string? Certification { get; set; }
    public string? YearOfExp { get; set; }
    public string? SeftDescription { get; set; }
}
