using System.Net;

namespace FSH.WebApi.Application.Common.Exceptions;

public class BadRequestException: CustomException
{
    public BadRequestException(string message)
        : base(message, null, HttpStatusCode.BadRequest)
    {
    }
}