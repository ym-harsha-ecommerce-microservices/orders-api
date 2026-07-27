using eCommerce.BLL.DTO.OrderItem;
using FluentValidation;

namespace eCommerce.BLL.Validators;

public class OrderItemUpdateRequestValidator : AbstractValidator<OrderItemUpdateRequest>
{
    public OrderItemUpdateRequestValidator()
    {
        RuleFor(x => x.ProductID)
            .NotEmpty().WithMessage("ProductID is required and cannot be empty.");

        RuleFor(x => x.UnitPrice)
            .NotNull().WithMessage("Unit price must be provided.")
            .GreaterThan(0).WithMessage("Unit price must be strictly greater than zero.");

        RuleFor(x => x.Quantity)
            .NotNull().WithMessage("Quantity must be provided.")
            .GreaterThan(0).WithMessage("Quantity must be at least 1.");
    }
}