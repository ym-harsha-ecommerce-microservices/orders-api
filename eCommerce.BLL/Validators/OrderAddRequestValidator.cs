using eCommerce.BLL.DTO.Order;
using FluentValidation;

namespace eCommerce.BLL.Validators;


public class OrderAddRequestValidator : AbstractValidator<OrderAddRequest>
{
    public OrderAddRequestValidator()
    {
        RuleFor(x => x.UserID)
            .NotEmpty().WithMessage("UserID is required.");

        RuleFor(x => x.OrderDate)
            .NotEmpty().WithMessage("Order date is required.")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Order date cannot be in the future.");

        RuleFor(x => x.OrderItems)
            .NotNull().WithMessage("Order items list is required.")
            .NotEmpty().WithMessage("The order must contain at least one item.");

        RuleForEach(x => x.OrderItems)
            .SetValidator(new OrderItemAddRequestValidator());
    }
}
