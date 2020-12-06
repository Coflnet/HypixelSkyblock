using dev;
using Microsoft.EntityFrameworkCore;
using System.Linq;


namespace hypixel
{
    public abstract class MyDbSet<TEntity> : DbSet<TEntity>  where TEntity : class
    {
        public TEntity GetOrCreateAndAdd(TEntity entity)
        {
            var value = this.Find(entity);
            if(value != null)
            {
                return value;
            }
            Add(entity);
            return entity;
        }

        public void AddOrUpdate(TEntity entity)
        {
            if(this.Contains(entity))
            {
                this.Update(entity);
            } else {
                this.Add(entity);
            }
        }
    }


    public class HypixelContext : DbContext {
        public DbSet<SaveAuction> Auctions { get; set; }

        public DbSet<SaveBids> Bids { get; set; }

        public DbSet<Player> Players {get;set;}

        public DbSet<ProductInfo> BazaarPrices {get;set;}
        public DbSet<BazaarPull> BazaarPull {get;set;}
        public DbSet<SubscribeItem> SubscribeItem {get;set;}

        protected override void OnConfiguring (DbContextOptionsBuilder optionsBuilder) {
            optionsBuilder.UseMySQL ("server=mariadb;database=test;user=root;password=takenfrombitnami; convert zero datetime=True;Charset=utf8");
                
        
        }

        protected override void OnModelCreating (ModelBuilder modelBuilder) {
            base.OnModelCreating (modelBuilder);
          

            modelBuilder.Entity<SaveAuction> (entity => {
                entity.HasIndex (e => e.Uuid).IsUnique();
                //entity.HasIndex (e => e.AuctioneerId);
                entity.HasIndex (e => e.AuctioneerIntId);
                entity.HasIndex(e=>e.ItemName);
                entity.HasIndex(e=>e.End);
                //entity.HasOne<NbtData>(d=>d.NbtData);
                //entity.HasMany<Enchantment>(e=>e.Enchantments);
                
            });

            modelBuilder.Entity<SaveBids> (entity => {
                entity.HasIndex (e => e.Bidder);
            });

            modelBuilder.Entity<NbtData> (entity => {
                entity.HasKey (e=>e.Id);
            });


            modelBuilder.Entity<ProductInfo>(entity=> {
                entity.HasKey(e=>e.Id);
                entity.HasIndex(e=>e.ProductId);
            });

            modelBuilder.Entity<BazaarPull>(entity=> {
                entity.HasKey(e=>e.Id);
                entity.HasIndex(e=>e.Timestamp);
            });


            modelBuilder.Entity<Player>(entity=> {
                entity.HasIndex(e=>e.UuId);
                entity.HasIndex(e=>e.Name);
                entity.HasKey(e=>e.Id);
            });


          



           
        }
    }
}