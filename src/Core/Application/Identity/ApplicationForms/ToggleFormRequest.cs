using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.ApplicationForms;
public class ToggleFormRequest
{
    public Guid FormId { get; set; }
    public string? Note { get; set; }
    public FormStatus Status { get; set; }
}
