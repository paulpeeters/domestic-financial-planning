namespace FinancialPlanningApp.Web.Services.Reconciliation;

public sealed class ReconciliationOptions
{
    public bool PrioritizeDepositsForPositiveAmounts { get; set; } = true;
    public bool PrioritizeCardSettlementByDescription { get; set; } = true;
    public string CardSettlementPrefix { get; set; } = "AFREKENING KREDIETKAARTEN";
}
