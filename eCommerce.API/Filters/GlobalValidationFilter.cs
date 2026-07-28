using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace eCommerce.API.Filters;

/// <summary>
/// Action filter that automatically validates all action arguments using their
/// registered FluentValidation validators before the action executes. If any
/// argument fails validation, the request is short-circuited with a 400 Bad Request.
/// </summary>
public class GlobalValidationFilter : IAsyncActionFilter
{
    /// <summary>
    /// Runs FluentValidation validators against every non-null action argument
    /// that has a registered <see cref="IValidator{T}"/>, aggregating all
    /// validation failures across arguments before returning a response.
    /// </summary>
    /// <param name="context">The context for the currently executing action, including its arguments.</param>
    /// <param name="next">The delegate that executes the next action filter or the action itself.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation. If validation fails,
    /// <paramref name="context"/>.Result is set to a <see cref="BadRequestObjectResult"/>
    /// and the pipeline is short-circuited; otherwise <paramref name="next"/> is invoked.
    /// </returns>
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