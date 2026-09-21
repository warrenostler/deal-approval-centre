using System;

namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// A single matched Budget (Dataverse 'goal') record for one BWG/BWT/BWY combination.
    /// </summary>
    public class BudgetRow
    {
        public Guid BudgetId { get; set; }

        public decimal CurrentYearBudget { get; set; }

        /// <summary>
        /// Null means the field is genuinely blank in Dataverse. A value of 0m means it was
        /// explicitly set to zero. The two are not the same for forecast-selection purposes.
        /// </summary>
        public decimal? Fc1 { get; set; }

        public decimal? Fc2 { get; set; }

        public decimal? Fc3 { get; set; }

        /// <summary>
        /// Base-currency Budget GP (fmi_currentyearbudgetgp_base) - Format Sale only. Null when
        /// blank on the Goal record.
        /// </summary>
        public decimal? BudgetGp { get; set; }
    }
}
