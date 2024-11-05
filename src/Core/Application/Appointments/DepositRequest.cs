using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public class DepositRequest
{
    public double? DepositAmount { get; set; }
    public double? RemainingAmount { get; set; }
}

public class DepositRequestValidator : CustomValidator<DepositRequest>
{
    public DepositRequestValidator()
    {
        RuleFor(p => p.DepositAmount)
            .Must(p => p > 0)
            .WithMessage("Deposit amount is not valid");

        RuleFor(p => p.RemainingAmount)
            .Must(p => p > 0)
            .WithMessage("Remaining amount is not valid");
    }
}
