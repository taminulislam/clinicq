namespace ClinicQ.Domain.Billing;

/// <summary>
/// Age-based patient discounts applied before tax. Kept deliberately small and explicit.
/// </summary>
public static class DiscountPolicy
{
    public const decimal SeniorDiscountPercent = 10m;
    public const decimal ChildDiscountPercent = 5m;
    public const int SeniorAge = 65;
    public const int ChildAge = 12;

    public static decimal ForPatientAge(int ageYears)
    {
        if (ageYears >= SeniorAge)
        {
            return SeniorDiscountPercent;
        }

        if (ageYears < ChildAge)
        {
            return ChildDiscountPercent;
        }

        return 0m;
    }
}
