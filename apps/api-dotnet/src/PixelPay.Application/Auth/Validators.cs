using System.Text.RegularExpressions;
using FluentValidation;
using PixelPay.Application.Common;

namespace PixelPay.Application.Auth;

// Rules are declared in Nest's output order: per property, decorators bottom-up.
public sealed partial class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator()
    {
        RuleFor(x => x.Email).Must(ClassValidator.IsEmail).WithMessage("email must be an email");

        RuleFor(x => x.Password)
            .Must(v => v.Text() is { } s && PasswordStrength().IsMatch(s))
            .WithMessage("password must contain uppercase, lowercase and number")
            .Must(v => ClassValidator.MaxLength(v, 72)).WithMessage("password must be shorter than or equal to 72 characters")
            .Must(v => ClassValidator.MinLength(v, 8)).WithMessage("password must be longer than or equal to 8 characters")
            .Must(ClassValidator.IsString).WithMessage("password must be a string");

        RuleFor(x => x.DisplayName)
            .Must(v => ClassValidator.MaxLength(v, 50)).WithMessage("displayName must be shorter than or equal to 50 characters")
            .Must(v => ClassValidator.MinLength(v, 2)).WithMessage("displayName must be longer than or equal to 2 characters")
            .Must(ClassValidator.IsString).WithMessage("displayName must be a string");

        RuleFor(x => x.ReferredBy)
            .Must(ClassValidator.IsString).WithMessage("referredBy must be a string")
            .When(x => !ClassValidator.IsMissing(x.ReferredBy));
    }

    // JS /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/: "." stops at line terminators and \d is ASCII.
    [GeneratedRegex(@"^(?=[^\n\r\u2028\u2029]*[a-z])(?=[^\n\r\u2028\u2029]*[A-Z])(?=[^\n\r\u2028\u2029]*[0-9])")]
    private static partial Regex PasswordStrength();
}

public sealed class RefreshTokenDtoValidator : AbstractValidator<RefreshTokenDto>
{
    public RefreshTokenDtoValidator() =>
        RuleFor(x => x.RefreshToken).Must(ClassValidator.IsString).WithMessage("refreshToken must be a string");
}
