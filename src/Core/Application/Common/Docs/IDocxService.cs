using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Common.Docs;
public interface IDocxService
{
    Task<byte[]> GenerateDocx(string htmlContent);
}
