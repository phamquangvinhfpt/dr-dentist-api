﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendar;
public class GetTimeWorkingRequest
{
    public string? UserID { get; set; }
    public DateOnly Date { get; set; }
}
