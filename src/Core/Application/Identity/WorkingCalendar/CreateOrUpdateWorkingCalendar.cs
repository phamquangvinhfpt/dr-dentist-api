using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendar;
public class CreateOrUpdateWorkingCalendar : IRequest<string>
{
    public DateTime Date { get; set; }
    public List<TimeWorkingRequest>? TimeWorkings { get; set; }
}
