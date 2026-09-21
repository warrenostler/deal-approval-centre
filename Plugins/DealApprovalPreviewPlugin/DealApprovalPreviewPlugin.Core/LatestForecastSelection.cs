namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// The result of applying the FC3 -&gt; FC2 -&gt; FC1 -&gt; 0 fallback rule.
    /// </summary>
    public class LatestForecastSelection
    {
        public decimal Value { get; set; }

        /// <summary>
        /// "FC1", "FC2", or "FC3" when a genuine forecast field was populated (including zero);
        /// empty string when none of FC1/FC2/FC3 was populated at all.
        /// </summary>
        public string Type { get; set; } = string.Empty;
    }
}
