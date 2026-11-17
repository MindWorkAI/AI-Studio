namespace AIStudio.Tools.Mathematics;

/// <summary>
/// The result of a numerical integration.
/// </summary>
/// <param name="Value">The estimate of the integral; only an estimate of it when Status is CONVERGED.</param>
/// <param name="EstimatedError">The estimated absolute error of the value.</param>
/// <param name="Status">How the integration ended.</param>
/// <param name="FailurePoint">Where the integrand had no finite value, when Status is NO_VALUE.</param>
/// <param name="Evaluations">How often the integrand was evaluated.</param>
public sealed record QuadratureResult(double Value, double EstimatedError, QuadratureStatus Status, double? FailurePoint, int Evaluations);