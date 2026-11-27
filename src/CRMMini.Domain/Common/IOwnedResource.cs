namespace CRMMini.Domain.Common;

public interface IOwnedResource
{
    Guid OwnerId { get; set; }
    Guid? TeamId { get; set; }
}
