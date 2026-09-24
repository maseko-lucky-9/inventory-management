using System.Text.RegularExpressions;
using FluentValidation;

namespace Inventory.Api.Shared.Validation;

/// <summary>The one definition of a valid product or warehouse code, shared by every validator that reads one.</summary>
public static partial class CodeRuleExtensions
{
    private const string Message = "'{PropertyName}' must be 1-50 letters, digits, '.', '_' or '-', starting with a letter or digit.";

    // NotEmpty first: under RuleLevelCascadeMode.Stop a missing code is one error, not also a pattern mismatch.
    public static IRuleBuilderOptions<T, string?> MustBeACode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().Must(BeAValidCode).WithMessage(Message);

    // Endpoints trim codes before the store sees them, so the pattern applies to the trimmed value.
    private static bool BeAValidCode(string? code) => code is not null && CodePattern().IsMatch(code.Trim());

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,49}$")]
    private static partial Regex CodePattern();
}
