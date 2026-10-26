using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PixelPay.Application.Common;

namespace PixelPay.IntegrationTests.Support;

public sealed record NamedDto(string? Name);

public sealed record LooseDto(string? Name);

public sealed class NamedDtoValidator : AbstractValidator<NamedDto>
{
    public NamedDtoValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("name should not be empty");
}

[ApiController]
[Route("api/v1/envelope-test")]
public class EnvelopeTestController : ControllerBase
{
    [HttpGet("object")]
    public object Obj() => new { id = 1, at = new DateTime(2026, 10, 26, 4, 0, 45, 100, DateTimeKind.Utc), amount = 12.50m };

    [HttpGet("null")]
    public object? Null() => null;

    [HttpGet("not-found")]
    public object ThrowNotFound() => throw new NotFoundException("Wallet not found");

    [HttpGet("unauthorized")]
    public object ThrowUnauthorized() => throw new UnauthorizedException();

    [HttpGet("crash")]
    public object Crash() => throw new InvalidOperationException("boom");

    [HttpPost("named")]
    public object Named(NamedDto dto) => dto;

    [HttpPost("loose")]
    public IActionResult Loose(LooseDto dto) => Ok(dto);
}
