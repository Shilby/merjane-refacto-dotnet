using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;
using Refacto.DotNet.Controllers.Services;

namespace Refacto.DotNet.Controllers.ProductRules
{
    public class SeasonalProductRules : ProductTypeRules
    {
        public SeasonalProductRules(INotificationService ns, AppDbContext ctx) : base(ns, ctx)
        {
        }

        public override void Process(Product p, DateTime now)
        {
            if (now.Date > p.SeasonStartDate && now.Date < p.SeasonEndDate && p.Available > 0)
            {
                Sell(p);
            }
            else if (now.AddDays(p.LeadTime) > p.SeasonEndDate)
            {
                _ns.SendOutOfStockNotification(p.Name);
                p.Available = 0;
                _ = _ctx.SaveChanges();
            }
            else if (p.SeasonStartDate > now)
            {
                _ns.SendOutOfStockNotification(p.Name);
                _ = _ctx.SaveChanges();
            }
            else
            {
                NotifyDelay(p.LeadTime, p);
            }
        }
    }
}
