namespace FinancialPlanningApp.Web.Services.Imports;

public interface IBankImportAdapter
{
    string ProviderKey { get; }
    bool CanHandle(string fileName);
    Task<IReadOnlyList<ImportedTransaction>> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
}
