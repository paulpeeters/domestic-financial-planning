using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Globalization;

namespace FinancialPlanningApp.Web.Extensions;

public sealed class FlexibleDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);
        var raw = valueResult.FirstValue;
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (Nullable.GetUnderlyingType(bindingContext.ModelType) is not null)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }

            return Task.CompletedTask;
        }

        if (TryParseFlexibleDecimal(raw, out var value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Enter a valid decimal amount.");
        return Task.CompletedTask;
    }

    private static bool TryParseFlexibleDecimal(string raw, out decimal value)
    {
        var normalized = raw.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("€", string.Empty, StringComparison.Ordinal);

        var commaIndex = normalized.LastIndexOf(',');
        var dotIndex = normalized.LastIndexOf('.');

        if (commaIndex >= 0 && dotIndex >= 0)
        {
            var decimalSeparator = commaIndex > dotIndex ? ',' : '.';
            var groupSeparator = decimalSeparator == ',' ? "." : ",";
            normalized = normalized.Replace(groupSeparator, string.Empty, StringComparison.Ordinal)
                .Replace(decimalSeparator, '.');
        }
        else
        {
            normalized = normalized.Replace(',', '.');
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }
}

public sealed class FlexibleDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var modelType = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return modelType == typeof(decimal) ? new FlexibleDecimalModelBinder() : null;
    }
}
