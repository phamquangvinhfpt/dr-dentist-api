using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendars;
public interface IWorkingCalendarService : ITransientService
{
    public List<WorkingCalendar> CreateWorkingCalendar(Guid doctorId, TimeSpan startTime, TimeSpan endTime, string? note = null);
    public List<WorkingCalendarResponse> GetWorkingCalendars(CancellationToken cancellation);
}
