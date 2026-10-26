using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using PixelPay.Application.Common;

namespace PixelPay.Application.Users;

// Rules are declared in Nest's output order: per property, decorators bottom-up.
public sealed class UpdateProfileDtoValidator : AbstractValidator<UpdateProfileDto>
{
    public UpdateProfileDtoValidator()
    {
        RuleFor(x => Present(x.DisplayName))
            .Must(v => ClassValidator.MaxLength(v, 50)).WithMessage("displayName must be shorter than or equal to 50 characters")
            .Must(v => ClassValidator.MinLength(v, 2)).WithMessage("displayName must be longer than or equal to 2 characters")
            .Must(ClassValidator.IsString).WithMessage("displayName must be a string")
            .When(x => !ClassValidator.IsMissing(Present(x.DisplayName)));

        RuleFor(x => Present(x.AvatarUrl))
            .Must(ClassValidator.IsString).WithMessage("avatarUrl must be a string")
            .When(x => !ClassValidator.IsMissing(Present(x.AvatarUrl)));
    }

    private static JsonElement? Present(JsonElement e) => e.ValueKind == JsonValueKind.Undefined ? null : e;
}

public sealed partial class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword).Must(ClassValidator.IsString).WithMessage("currentPassword must be a string");

        RuleFor(x => x.NewPassword)
            .Must(v => v.Text() is { } s && PasswordStrength().IsMatch(s))
            .WithMessage("newPassword must match /^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)/ regular expression")
            .Must(v => ClassValidator.MaxLength(v, 72)).WithMessage("newPassword must be shorter than or equal to 72 characters")
            .Must(v => ClassValidator.MinLength(v, 8)).WithMessage("newPassword must be longer than or equal to 8 characters")
            .Must(ClassValidator.IsString).WithMessage("newPassword must be a string");
    }

    // JS /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/: "." stops at line terminators and \d is ASCII.
    [GeneratedRegex(@"^(?=[^\n\r  ]*[a-z])(?=[^\n\r  ]*[A-Z])(?=[^\n\r  ]*[0-9])")]
    private static partial Regex PasswordStrength();
}
