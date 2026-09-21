using System;
using System.Globalization;

namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Formats the optional "Licence: start to end" line used in the legacy fmi_submitteddealitems
    /// snapshot text, restoring the deployed JS's licence-date behaviour for backward compatibility
    /// with the Power Automate approval notification that still consumes this field. Dataverse-
    /// independent and deterministic (no client-local timezone formatting) so both the date format
    /// and the "only show the line when at least one date is present" rule can be unit tested
    /// without an IOrganizationService.
    /// </summary>
    public static class LicenceSnapshotFormatter
    {
        private const string DateFormat = "dd MMM yyyy";
        private const string MissingDatePlaceholder = "-";

        public static string FormatDate(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString(DateFormat, CultureInfo.InvariantCulture)
                : MissingDatePlaceholder;
        }

        /// <summary>
        /// Returns null (meaning: omit the line entirely) when neither date is present, mirroring
        /// the old JS's "if (item.licenceStart || item.licenceEnd)" guard.
        /// </summary>
        public static string FormatLicenceLine(DateTime? licenceStart, DateTime? licenceEnd)
        {
            if (!licenceStart.HasValue && !licenceEnd.HasValue)
            {
                return null;
            }

            return $"Licence: {FormatDate(licenceStart)} to {FormatDate(licenceEnd)}";
        }
    }
}
