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
            string s => DateOnly.Parse(s, CultureInfo.InvariantCulture),
            _ => DateOnly.FromDateTime((DateTime)value)
        };
    }
}
