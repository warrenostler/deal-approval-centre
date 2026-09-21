namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// The fully computed financial preview for one Opportunity Item. Budget/LatestForecast/
    /// VarianceToForecast/VarianceToBudget are null when no financial comparison could be made
    /// (missing BWG/BWT mapping or no matching Goal/Budget record) - see
    /// <see cref="FinancialComparisonAvailable"/> and <see cref="Unavailable"/>. This is distinct
    /// from a Goal/Budget record existing with FC1/FC2/FC3 all blank, where LatestForecast is a
    /// genuine 0 and FinancialComparisonAvailable stays true.
    /// </summary>
    public class ItemFinancialResult
    {
        public decimal Sale { get; set; }

        public decimal? Budget { get; set; }

        /// <summary>
        /// Base-currency GP fields - Format Sale only, always null for every other Sales Type.
        /// SaleGp is populated whenever an item is resolved, even when the Budget comparison is
        /// unavailable, mirroring how Sale itself is always populated.
        /// </summary>
        public decimal? SaleGp { get; set; }

        public decimal? BudgetGp { get; set; }

        public decimal? Fc1 { get; set; }

        public decimal? Fc2 { get; set; }

        public decimal? Fc3 { get; set; }

        public decimal? LatestForecast { get; set; }

        public string LatestForecastType { get; set; } = string.Empty;

        public decimal? VarianceToForecast { get; set; }

        public decimal? VarianceToBudget { get; set; }

        public bool BelowForecast { get; set; }

        /// <summary>
        /// False when Budget/LatestForecast/etc. could not be resolved (see
        /// <see cref="FinancialWarning"/> for why) - true otherwise, including the "Goal exists
        /// but no FC populated" case.
        /// </summary>
        public bool FinancialComparisonAvailable { get; set; } = true;

        /// <summary>
        /// Human-readable explanation of why FinancialComparisonAvailable is false. Null/empty
        /// when a comparison was made (successfully or otherwise resolved).
        /// </summary>
        public string FinancialWarning { get; set; }

        /// <summary>
        /// Builds the result for an Opportunity Item whose financial comparison could not be
        /// made (missing BWG/BWT mapping, or no matching Goal/Budget record). Sale is preserved;
        /// every comparison field is left null and BelowForecast is false - a missing comparison
        /// is never treated as "below forecast".
        /// </summary>
        public static ItemFinancialResult Unavailable(decimal sale, string warning)
        {
            return new ItemFinancialResult
            {
                Sale = sale,
                BelowForecast = false,
                FinancialComparisonAvailable = false,
                FinancialWarning = warning
            };
        }

        /// <summary>
        /// Same as <see cref="Unavailable(decimal, string)"/> but preserves the item's own Sale
        /// GP, which is independent of whether a Budget comparison could be made.
        /// </summary>
        public static ItemFinancialResult Unavailable(decimal sale, decimal? saleGp, string warning)
        {
            var result = Unavailable(sale, warning);
            result.SaleGp = saleGp;
            return result;
        }
    }
}
