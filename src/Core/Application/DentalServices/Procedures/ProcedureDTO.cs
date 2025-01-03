﻿using FSH.WebApi.Application.DentalServices.Services;
using FSH.WebApi.Domain.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices.Procedures;
public class ProcedureDTO
{
    public Guid ProcedureID { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public double Price { get; set; }
    public string? CreateBy { get; set; }
    public DateTime CreateDate { get; set; }
}
