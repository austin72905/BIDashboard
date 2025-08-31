using System.Text.Json.Serialization;

namespace BIDashboardBackend.DTOs.Response
{
    public class FirebaseAuthResponse
    {
        [JsonPropertyName("users")]
        public List<FirebaseUser> Users { get; set; }
    }

    public class FirebaseUser
    {
        [JsonPropertyName("localId")]
        public string Uid { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("providerUserInfo")]
        public List<ProviderUserInfo> ProviderUserInfos { get; set; }
    }

    public class ProviderUserInfo
    {
        [JsonPropertyName("providerId")]
        public string ProviderId { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("photoUrl")]
        public string PhotoUrl { get; set; }

        [JsonPropertyName("federatedId")]
        public string FederatedId { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("rawId")]
        public string RawId { get; set; }

        [JsonPropertyName("screenName")]
        public string ScreenName { get; set; }
    }
}
