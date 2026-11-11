using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SchoolAdmission.Api.Common;

public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is AdmissionAppException appEx)
        {
            _logger.LogWarning("Handled application exception ({StatusCode}): {Message}",
                appEx.StatusCode, appEx.Message);

            var problemDetails = new ProblemDetails
            {
                Status = (int)appEx.StatusCode,
                Title = appEx.GetType().Name.Replace("Exception", string.Empty),
                Detail = appEx.Message
            };

            context.Result = new ObjectResult(problemDetails)
            {
                StatusCode = (int)appEx.StatusCode
            };
            context.ExceptionHandled = true;
            return;
        }

        _logger.LogError(context.Exception, "Unhandled system exception occurred: {Message}", context.Exception.Message);

        var genericDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = "An unexpected error occurred while processing your request."
        };

        context.Result = new ObjectResult(genericDetails)
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
        context.ExceptionHandled = true;
    }
}

