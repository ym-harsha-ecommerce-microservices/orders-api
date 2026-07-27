using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace eCommerce.API.Filters;

public class GlobalValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var allFailures = new List<ValidationFailure>();

        foreach (var argument in context.ActionArguments.Values.Where(v => v != null))
        {
            var argumentType = argument!.GetType();

            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);

            var validator = context.HttpContext.RequestServices.GetService(validatorType) as IValidator;

            if (validator != null)
            {
                var validationContext = new ValidationContext<object>(argument);
                var validationResult = await validator.ValidateAsync(validationContext);

                if (!validationResult.IsValid)
                {
                    allFailures.AddRange(validationResult.Errors);
                }
            }
        }

        if (allFailures.Count > 0)
        {
            var validationResult = new ValidationResult(allFailures);
            context.Result = new BadRequestObjectResult(validationResult.ToDictionary());
            return;
        }


        await next();
    }
}
