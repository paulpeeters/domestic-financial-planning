using FinancialPlanningApp.Web.Services.Imports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace FinancialPlanningApp.Web.Pages.Imports;

[Authorize]
public class IndexModel(IBankImportService bankImportService) : PageModel
{
    private static readonly IReadOnlyList<SelectListItem> ProviderOptionsList =
    [
        new("CODA (.coda/.txt)", "CODA"),
        new("Kredietkaart PDF (Europabank Visa)", "CREDITCARD_PDF_EUROPABANK_VISA")
    ];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public ConfirmInputModel ConfirmInput { get; set; } = new();

    public ImportResult? Result { get; private set; }
    public IReadOnlyList<ImportedTransaction> PreviewTransactions { get; private set; } = [];
    public string? PreviewMessage { get; private set; }
    public int PreviewDebitCount { get; private set; }
    public int PreviewCreditCount { get; private set; }
    public decimal PreviewDebitSum { get; private set; }
    public decimal PreviewCreditSum { get; private set; }
    public IReadOnlyList<SelectListItem> ProviderOptions => ProviderOptionsList;

    public sealed class InputModel
    {
        [Required]
        [Display(Name = "Importprovider")]
        public string ProviderKey { get; set; } = "CODA";

        [Required]
        [Display(Name = "Importbestand(en)")]
        public List<IFormFile> Files { get; set; } = [];
    }

    public sealed class ConfirmInputModel
    {
        public string ProviderKey { get; set; } = "CODA";
        public string FileName { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        ModelState.Remove("ConfirmInput.ProviderKey");
        ModelState.Remove("ConfirmInput.FileName");
        ModelState.Remove("ConfirmInput.PayloadJson");

        if (!ModelState.IsValid)
        {
            Input.ProviderKey = string.IsNullOrWhiteSpace(Input.ProviderKey) ? "CODA" : Input.ProviderKey;
            return Page();
        }

        if (Input.Files.Count == 0 || Input.Files.All(f => f.Length == 0))
        {
            ModelState.AddModelError(string.Empty, "Selecteer minstens een niet-leeg bestand.");
            Input.ProviderKey = string.IsNullOrWhiteSpace(Input.ProviderKey) ? "CODA" : Input.ProviderKey;
            return Page();
        }

        var parsed = new List<ImportedTransaction>();
        var fileNames = new List<string>();
        foreach (var file in Input.Files.Where(f => f.Length > 0))
        {
            await using var stream = file.OpenReadStream();
            var oneFileParsed = await bankImportService.ParseAsync(Input.ProviderKey, stream, file.FileName, cancellationToken);
            parsed.AddRange(oneFileParsed);
            fileNames.Add(file.FileName);
        }

        PreviewTransactions = parsed.Take(200).ToList();

        if (parsed.Count == 0)
        {
            PreviewMessage = $"Er werden geen transacties gelezen voor provider '{Input.ProviderKey}' uit de geselecteerde bestand(en).";
            return Page();
        }

        var transactional = parsed.Where(x => !x.IsInformational).ToList();
        PreviewDebitCount = transactional.Count(x => x.Amount < 0);
        PreviewCreditCount = transactional.Count(x => x.Amount > 0);
        PreviewDebitSum = transactional.Where(x => x.Amount < 0).Sum(x => x.Amount);
        PreviewCreditSum = transactional.Where(x => x.Amount > 0).Sum(x => x.Amount);

        PreviewMessage = $"{parsed.Count} transactie(s) gelezen uit {fileNames.Count} bestand(en). Er worden maximaal 200 transacties getoond.";

        ConfirmInput = new ConfirmInputModel
        {
            ProviderKey = Input.ProviderKey,
            FileName = string.Join("; ", fileNames),
            PayloadJson = JsonSerializer.Serialize(parsed)
        };

        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync(CancellationToken cancellationToken)
    {
        Input.ProviderKey = string.IsNullOrWhiteSpace(ConfirmInput.ProviderKey) ? "CODA" : ConfirmInput.ProviderKey;

        if (string.IsNullOrWhiteSpace(ConfirmInput.ProviderKey) ||
            string.IsNullOrWhiteSpace(ConfirmInput.FileName) ||
            string.IsNullOrWhiteSpace(ConfirmInput.PayloadJson))
        {
            ModelState.AddModelError(string.Empty, "Previewdata ontbreekt. Voer de preview opnieuw uit.");
            return Page();
        }

        var parsed = JsonSerializer.Deserialize<List<ImportedTransaction>>(ConfirmInput.PayloadJson);
        if (parsed is null || parsed.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "De import-previewdata is leeg of ongeldig.");
            return Page();
        }

        Result = await bankImportService.PersistAsync(ConfirmInput.ProviderKey, parsed, ConfirmInput.FileName, cancellationToken);
        return Page();
    }
}
