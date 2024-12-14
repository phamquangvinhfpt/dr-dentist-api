using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Identity;

public class ContactFormEmail
{
    public string? Email { get; set; }
    public string? ConsultContent { get; set; }
    public string? Phone { get; set; }
    public string? ClinicPhone { get; set; }
    public string? ClinicAddress { get; set; }
}
