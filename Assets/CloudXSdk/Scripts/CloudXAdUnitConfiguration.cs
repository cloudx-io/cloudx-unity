#nullable enable

namespace CloudX
{
    /// <summary>Session configuration for one CloudX ad unit.</summary>
    /// <param name="IsOrchestratorEnabled">
    /// Whether Orchestrator owns loading for this unit. This describes configuration,
    /// not mediator readiness or ad availability.
    /// </param>
    public record CloudXAdUnitConfiguration(bool IsOrchestratorEnabled);
}
