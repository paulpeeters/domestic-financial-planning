namespace FinancialPlanningApp.Web.Services.Imports;

public interface IBankImportService
{
    Task<IReadOnlyList<ImportedTransaction>> ParseAsync(string providerKey, Stream stream, string fileName, CancellationToken cancellationToken = default);
    Task<ImportResult> PersistAsync(string providerKey, IReadOnlyList<ImportedTransaction> transactions, string fileName, CancellationToken cancellationToken = default);
}
