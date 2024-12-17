using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.CustomerServices;
public class UpdateEmailContextRequest
{
    public Guid ContactID { get; set; }
    public string? EmailContext { get; set; }
}
