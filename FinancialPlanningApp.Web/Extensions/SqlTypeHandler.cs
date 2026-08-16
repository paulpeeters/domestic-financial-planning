using Dapper;
using System.Data;
using System.Globalization;

namespace FinancialPlanningApp.Web.Extensions
{
    /// <summary>
    /// Provides a Dapper type handler for mapping between the <see cref="DateOnly"/> type and SQL date values.
    /// </summary>
    /// <remarks>When storing, the time component is set to midnight. Reads accept both <see cref="DateTime"/> and string values.</remarks>
    public class SqlDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly date)
            => parameter.Value = date.ToDateTime(TimeOnly.MinValue);

        public override DateOnly Parse(object value) => value switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            string s when DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) => date,
            string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime) => DateOnly.FromDateTime(dateTime),
            _ => DateOnly.FromDateTime((DateTime)value)
        };
    }

    public sealed class SqlDecimalTypeHandler : SqlMapper.TypeHandler<decimal>
    {
        public override void SetValue(IDbDataParameter parameter, decimal value)
            => parameter.Value = value;

        public override decimal Parse(object value) => value switch
        {
            decimal d => d,
            double d => Convert.ToDecimal(d, CultureInfo.InvariantCulture),
            float f => Convert.ToDecimal(f, CultureInfo.InvariantCulture),
            long l => l,
            int i => i,
            string s when decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant) => invariant,
            string s when decimal.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out var current) => current,
            _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
        };
    }
}
