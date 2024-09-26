using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Common.Validation;
public static class CheckEmailAddressOrPhoneNumberExtension
{
    public static ValidationType CheckType(this string request)
    {
        if (IsPhoneNumber(request))
            return ValidationType.PhoneNumber;
        else if (IsEmailAddress(request))
            return ValidationType.EmailAddress;
        else
            return ValidationType.Unknow;
    }

    private static bool IsEmailAddress(string request)
    {
        // Biểu thức chính quy để kiểm tra định dạng email
        string pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

        // Kiểm tra xem chuỗi request khớp với biểu thức chính quy không
        return Regex.IsMatch(request, pattern);
    }

    private static bool IsPhoneNumber(string request)
    {
        foreach (char c in request)
        {
            if (!char.IsDigit(c))
                return false;
        }

        return request.Length == 10;
    }
}

public enum ValidationType
{
    Unknow,
    PhoneNumber,
    EmailAddress,
}
