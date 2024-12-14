using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.CustomerServices;

public class UpdatePhoneCallImageRequest
{
    public Guid ContactID { get; set; }
    public IFormFile[]? Images { get; set; }
}
