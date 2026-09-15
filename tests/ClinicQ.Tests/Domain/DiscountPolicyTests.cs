using ClinicQ.Domain.Billing;

namespace ClinicQ.Tests.Domain;

public class DiscountPolicyTests
{
    // Attribute arguments cannot be decimal, so the expected percentages are passed as doubles.
    [Theory]
    [InlineData(0, 5.0)]    // child
    [InlineData(11, 5.0)]
    [InlineData(12, 0.0)]   // standard rate starts at 12
    [InlineData(40, 0.0)]
    [InlineData(64, 0.0)]
    [InlineData(65, 10.0)]  // senior
    [InlineData(90, 10.0)]
    public void Discount_depends_on_the_patient_age(int age, double expected)
        => Assert.Equal((decimal)expected, DiscountPolicy.ForPatientAge(age));

    [Fact]
    public void Policy_thresholds_match_the_documented_rates()
    {
        Assert.Equal(DiscountPolicy.ChildDiscountPercent, DiscountPolicy.ForPatientAge(DiscountPolicy.ChildAge - 1));
        Assert.Equal(0m, DiscountPolicy.ForPatientAge(DiscountPolicy.ChildAge));
        Assert.Equal(DiscountPolicy.SeniorDiscountPercent, DiscountPolicy.ForPatientAge(DiscountPolicy.SeniorAge));
    }
}
