using CustomFumenProviderWebServer.Models.Tables;

namespace CustomFumenProviderWebServer.Models.Responses
{
    public class DeliverResultResponse
    {
        public DeliverResultResponse(bool isSuccess, string message = "", FumenSet fumenSet = default)
        {
            IsSuccess = isSuccess;
            Message = message;
            FumenSet = fumenSet;
        }

        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public FumenSet FumenSet { get; set; }
    }
}
