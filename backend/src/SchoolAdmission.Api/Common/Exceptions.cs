using System.Net;

namespace SchoolAdmission.Api.Common;

public abstract class AdmissionAppException : Exception
{
    public HttpStatusCode StatusCode { get; }

    protected AdmissionAppException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

public class NotFoundException : AdmissionAppException
{
    public NotFoundException(string message)
        : base(message, HttpStatusCode.NotFound)
    {
    }
}

public class ConflictException : AdmissionAppException
{
    public ConflictException(string message)
        : base(message, HttpStatusCode.Conflict)
    {
    }
}

public class ForbiddenException : AdmissionAppException
{
    public ForbiddenException(string message)
        : base(message, HttpStatusCode.Forbidden)
    {
    }
}

public class BadRequestException : AdmissionAppException
{
    public BadRequestException(string message)
        : base(message, HttpStatusCode.BadRequest)
    {
    }
}

