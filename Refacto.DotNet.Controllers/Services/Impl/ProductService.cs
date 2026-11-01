using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;

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
            if (p.CanBeSold(now))
            {
                p.Sell();
                _ = _ctx.SaveChanges();
                return;
            }

            switch (p.Type)
            {
                case ProductType.Normal:
                    if (p.LeadTime > 0)
                    {
                        NotifyDelay(p.LeadTime, p);
                    }
                    break;
                case ProductType.Seasonal:
                    HandleSeasonalProduct(p, now);
                    break;
                case ProductType.Expirable:
                    HandleExpiredProduct(p);
                    break;
            }
        }

        public void NotifyDelay(int leadTime, Product p)
        {
            p.LeadTime = leadTime;
            _ = _ctx.SaveChanges();
            _ns.SendDelayNotification(leadTime, p.Name);
        }

        private void HandleSeasonalProduct(Product p, DateTime now)
        {
            if (now.AddDays(p.LeadTime) > p.SeasonEndDate)
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

        private void HandleExpiredProduct(Product p)
        {
            _ns.SendExpirationNotification(p.Name, (DateTime)p.ExpiryDate);
            p.Available = 0;
            _ = _ctx.SaveChanges();
        }
    }
}
