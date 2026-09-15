namespace ClinicQ.Domain.Billing;

/// <summary>A service to be billed, referenced by fee-schedule code.</summary>
public sealed record ServiceLineRequest(string ServiceCode, int Quantity = 1);
