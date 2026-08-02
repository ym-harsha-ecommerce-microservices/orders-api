using Polly;

namespace eCommerce.BLL.Policies.Interfaces;

public interface IProductsMicroservicePolicies
{
    IAsyncPolicy<HttpResponseMessage> GetProductsPolicies();
}
