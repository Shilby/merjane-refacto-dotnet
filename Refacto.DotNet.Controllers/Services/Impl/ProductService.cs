using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.ProductRules;

namespace Refacto.DotNet.Controllers.Services.Impl
{
    public class ProductService
    {
        private readonly INotificationService _ns;
        private readonly AppDbContext _ctx;

        public ProductService(INotificationService ns, AppDbContext ctx)
        {
            _ns = ns;
            _ctx = ctx;
        }

        public void ProcessOrderedProduct(Product p, DateTime now)
        {
            ProductTypeRulesFactory.Create(p.Type, _ns, _ctx)?.Process(p, now);
        }

        public void NotifyDelay(int leadTime, Product p)
        {
            new NormalProductRules(_ns, _ctx).NotifyDelay(leadTime, p);
        }
    }
}
