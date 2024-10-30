﻿using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendars;
public interface IWorkingCalendarService : ITransientService
{
    public List<WorkingCalendar> CreateWorkingCalendar(string doctorId, TimeSpan startTime, TimeSpan endTime, string? note = null);
    public List<WorkingCalendar> GetWorkingCalendars(string doctorId, CancellationToken cancellation);
}