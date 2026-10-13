using System.ComponentModel.DataAnnotations;

namespace Banking.Api.Contracts;

public sealed class CustomerLoginRequest
{
    [Required, StringLength(254)]
    public string Identifier { get; init; } = null!;

    [Required, RegularExpression("[0-9]{6}")]
    public string Pin { get; init; } = null!;

    public override string ToString() => "CustomerLoginRequest(credentials=redacted)";
}
