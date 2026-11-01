using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;

namespace Refacto.DotNet.Controllers.ProductRules
{
    public abstract class ProductTypeRules
    {
        protected readonly INotificationService _ns;
        protected readonly AppDbContext _ctx;

        protected ProductTypeRules(INotificationService ns, AppDbContext ctx)
        {
            _ns = ns;
            _ctx = ctx;
        }

        public abstract void Process(Product p, DateTime now);

        public void NotifyDelay(int leadTime, Product p)
        {
            p.LeadTime = leadTime;
            _ = _ctx.SaveChanges();
            _ns.SendDelayNotification(leadTime, p.Name);
        }

        protected void Sell(Product p)
        {
            p.Sell();
            _ = _ctx.SaveChanges();
        }
    }
}
