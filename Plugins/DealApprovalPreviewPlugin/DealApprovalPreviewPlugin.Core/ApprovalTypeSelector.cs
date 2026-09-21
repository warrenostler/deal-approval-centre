using System;

namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Maps Opportunity stage option values to Deal Approval approval type option values.
    /// </summary>
    public static class ApprovalTypeSelector
    {
        public const int InitialApproval = 1;
        public const int ReapprovalPreAuthorisation = 2;
        public const int ReapprovalPostAuthorisation = 3;

        public const string OpportunityStageUnauthorised = "Unauthorised";
        public const string OpportunityStageAuthorised = "Authorised";
        public const string OpportunityStageOffer = "Offer";

        public const int OpportunityStageValueUnauthorised = 797300000;
        public const int OpportunityStageValueAuthorised = 797300001;
        public const int OpportunityStageValueOffer = 797300003;

        public static int Determine(string opportunityStageName)
        {
            switch (opportunityStageName)
            {
                case OpportunityStageOffer:
                    return InitialApproval;
                case OpportunityStageUnauthorised:
                    return ReapprovalPreAuthorisation;
                case OpportunityStageAuthorised:
                    return ReapprovalPostAuthorisation;
                default:
                    throw new InvalidOperationException(
                        "The active Opportunity BPF stage must be Offer, Unauthorised, or Authorised before approval can be submitted.");
            }
        }

        public static int Determine(int opportunityStageValue)
        {
            switch (opportunityStageValue)
            {
                case OpportunityStageValueOffer:
                    return InitialApproval;
                case OpportunityStageValueUnauthorised:
                    return ReapprovalPreAuthorisation;
                case OpportunityStageValueAuthorised:
                    return ReapprovalPostAuthorisation;
                default:
                    throw new InvalidOperationException(
                        "The Opportunity stage must be Offer, Unauthorised, or Authorised before approval can be submitted.");
            }
        }
    }
}
