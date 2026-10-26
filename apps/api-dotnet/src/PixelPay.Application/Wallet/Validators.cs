using FluentValidation;
using PixelPay.Application.Common;

namespace PixelPay.Application.Wallets;

// Rules are declared in Nest's output order: per property, decorators bottom-up.
public sealed class DepositDtoValidator : AbstractValidator<DepositDto>
{
    public DepositDtoValidator()
    {
        // @IsNotEmpty(): not "", null or undefined.
        RuleFor(x => x.UserId)
            .Must(v => !ClassValidator.IsMissing(v) && v.Text() != "").WithMessage("userId should not be empty")
            .Must(ClassValidator.IsString).WithMessage("userId must be a string");

        RuleFor(x => x.Amount)
            .Must(ClassValidator.IsPositive).WithMessage("amount must be a positive number")
            .Must(ClassValidator.IsNumber).WithMessage("amount must be a number conforming to the specified constraints");

        RuleFor(x => x.Description)
            .Must(ClassValidator.IsString).WithMessage("description must be a string")
            .When(x => !ClassValidator.IsMissing(x.Description));
    }
}
