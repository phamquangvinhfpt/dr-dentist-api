using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.ApplicationForms;
public class AddFormRequest
{
    public string? UserID { get; set; }
    public Guid CalendarID { get; set; }
    public Guid TimeID { get; set; }
    public string? Description { get; set; }
}
