using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users;
public class UpdateMedicalHistoryRequest
{
    public string? PatientId { get; set; }
    public string[] MedicalName { get; set; } = Array.Empty<string>();
    public string? Note { get; set; }
}
