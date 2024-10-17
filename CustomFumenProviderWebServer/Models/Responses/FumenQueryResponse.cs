using CustomFumenProviderWebServer.Models.Tables;

namespace CustomFumenProviderWebServer.Models.Responses
{
    public class FumenQueryResponse
    {
        public List<FumenSet> FumenSets { get; set; }

        public int QueryResultTotal { get; set; }
    }
}
