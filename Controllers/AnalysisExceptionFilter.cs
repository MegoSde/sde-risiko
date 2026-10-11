using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AlpacaMedic.Controllers;
// Translate expected analysis errors to JSON; unexpected exceptions use the global handler.
public class AnalysisExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
            ValidationException => 400,
            KeyNotFoundException => 404,
            _ => 0
        };
        if (status == 0)
            return;
        context.Result = new ObjectResult(new { error = context.Exception.Message })
        {
            StatusCode = status
        };
        context.ExceptionHandled = true;
    }
}
