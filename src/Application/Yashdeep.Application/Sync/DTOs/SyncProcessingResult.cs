using Yashdeep.Domain.Sync;

namespace Yashdeep.Application.Sync.DTOs
{
    public sealed class SyncProcessingResult<TResponse>
    {
        public bool IsSuccess { get; set; }
        public bool IsDuplicate { get; set; }
        public InboxStatus Status { get; set; }
        public TResponse? Data { get; set; }
        public string? ErrorMessage { get; set; }

        public static SyncProcessingResult<TResponse> Success(TResponse data, bool isDuplicate = false) =>
            new()
            {
                IsSuccess = true,
                IsDuplicate = isDuplicate,
                Status = InboxStatus.Processed,
                Data = data
            };

        public static SyncProcessingResult<TResponse> Duplicate(TResponse data) =>
            Success(data, isDuplicate: true);

        public static SyncProcessingResult<TResponse> Failure(InboxStatus status, string errorMessage) =>
            new()
            {
                IsSuccess = false,
                IsDuplicate = false,
                Status = status,
                ErrorMessage = errorMessage
            };
    }
}
