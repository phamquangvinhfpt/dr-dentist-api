using FSH.WebApi.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Payments;
public class PaymentDetailResponse
{
    public PaymentResponse? PaymentResponse { get; set; }
    public List<PaymentDetail>? Details { get; set; }
}
