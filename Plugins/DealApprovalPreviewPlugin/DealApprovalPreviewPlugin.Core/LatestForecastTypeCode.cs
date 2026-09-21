namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Maps FinancialCalculator's FC1/FC2/FC3/blank label to the fmi_dealapprovalitem
    /// Latest Forecast Type option set values (1/2/3), confirmed from the fmi_DealApprovalItem
    /// solution metadata. A blank label (no FC field was ever populated) has no corresponding
    /// option - callers should leave the picklist unset rather than write a fabricated value.
    /// </summary>
    public static class LatestForecastTypeCode
    {
        public const int Fc1 = 1;
        public const int Fc2 = 2;
        public const int Fc3 = 3;

        public static int? Resolve(string latestForecastType)
        {
            if (latestForecastType == FinancialCalculator.Fc1Label)
            {
                return Fc1;
            }

            if (latestForecastType == FinancialCalculator.Fc2Label)
            {
                return Fc2;
            }

            if (latestForecastType == FinancialCalculator.Fc3Label)
            {
                return Fc3;
            }

            return null;
        }
    }
}
