namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Pure financial-selection and variance calculations for the Deal Approval preview.
    /// Mirrors the rules in the existing fmi_DealApprovalCommands JavaScript
    /// (LATEST_FORECAST_TYPE / getLatestForecast and the Deal Approval Item variance block).
    /// Contains no Dataverse dependency so it can be unit tested in isolation.
    /// </summary>
    public static class FinancialCalculator
    {
        public const string Fc1Label = "FC1";
        public const string Fc2Label = "FC2";
        public const string Fc3Label = "FC3";

        /// <summary>
        /// FC3 if populated, otherwise FC2 if populated, otherwise FC1 if populated, otherwise 0.
        /// A field is "populated" whenever it is not null - an explicit value of zero counts.
        /// </summary>
        public static LatestForecastSelection SelectLatestForecast(decimal? fc1, decimal? fc2, decimal? fc3)
        {
            if (fc3.HasValue)
            {
                return new LatestForecastSelection { Value = fc3.Value, Type = Fc3Label };
            }

            if (fc2.HasValue)
            {
                return new LatestForecastSelection { Value = fc2.Value, Type = Fc2Label };
            }

            if (fc1.HasValue)
            {
                return new LatestForecastSelection { Value = fc1.Value, Type = Fc1Label };
            }

            return new LatestForecastSelection { Value = 0m, Type = string.Empty };
        }

        /// <summary>
        /// Sale and Budget are treated as plain amounts (a blank Dataverse Money value is
        /// coalesced to 0 by the caller before reaching here) - neither has a "zero means
        /// something different from blank" rule, unlike FC1/FC2/FC3.
        /// </summary>
        public static ItemFinancialResult Calculate(decimal sale, BudgetRow budget)
        {
            var forecast = SelectLatestForecast(budget.Fc1, budget.Fc2, budget.Fc3);

            var belowForecast = forecast.Type != string.Empty && sale < forecast.Value;

            return new ItemFinancialResult
            {
                Sale = sale,
                Budget = budget.CurrentYearBudget,
                Fc1 = budget.Fc1,
                Fc2 = budget.Fc2,
                Fc3 = budget.Fc3,
                LatestForecast = forecast.Value,
                LatestForecastType = forecast.Type,
                VarianceToForecast = sale - forecast.Value,
                VarianceToBudget = sale - budget.CurrentYearBudget,
                BelowForecast = belowForecast
            };
        }
    }
}
