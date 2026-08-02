using Polly;
using System;
using System.Collections.Generic;
using System.Text;

namespace eCommerce.BLL.Policies.Interfaces;

public interface IUsersMicroservicePolicies
{
    IAsyncPolicy<HttpResponseMessage> GetUsersPolicies();
}
