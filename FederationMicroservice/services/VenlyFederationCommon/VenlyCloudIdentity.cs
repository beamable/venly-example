using Beamable.Common;

namespace VenlyFederationCommon
{
    /// <summary>
    /// Venly Cloud Identity
    /// </summary>
    public class VenlyCloudIdentity : IThirdPartyCloudIdentity
    {
        /// <summary>
        /// Namespace key
        /// </summary>
        public string UniqueName => "venly";
    }
}