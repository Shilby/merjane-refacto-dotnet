using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;

namespace Refacto.DotNet.Controllers.ProductRules
{
    public class NormalProductRules : ProductTypeRules
    {
        public NormalProductRules(INotificationService ns, AppDbContext ctx) : base(ns, ctx)
        {
        }

        public override void Process(Product p, DateTime now)
        {
            if (p.Available > 0)
            {
                Sell(p);
            }
            else if (p.LeadTime > 0)
            {
                NotifyDelay(p.LeadTime, p);
            }
        }
    }
}
