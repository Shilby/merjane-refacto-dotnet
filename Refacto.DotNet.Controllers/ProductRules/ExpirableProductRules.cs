using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;

namespace Refacto.DotNet.Controllers.ProductRules
{
    public class ExpirableProductRules : ProductTypeRules
    {
        public ExpirableProductRules(INotificationService ns, AppDbContext ctx) : base(ns, ctx)
        {
        }

        public override void Process(Product p, DateTime now)
        {
            if (p.Available > 0 && p.ExpiryDate > now.Date)
            {
                Sell(p);
            }
            else
            {
                _ns.SendExpirationNotification(p.Name, (DateTime)p.ExpiryDate);
                p.Available = 0;
                _ = _ctx.SaveChanges();
            }
        }
    }
}
