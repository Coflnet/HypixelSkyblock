using dev;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Coflnet.Sky.Core
{
    public abstract class MyDbSet<TEntity> : DbSet<TEntity> where TEntity : class
    {
        public TEntity GetOrCreateAndAdd(TEntity entity)
        {
            var value = Find(entity);
            if (value != null)
            {
                return value;
            }
            Add(entity);
            return entity;
        }

        public void AddOrUpdate(TEntity entity)
        {
            if (this.Contains(entity))
            {
                Update(entity);
            }
            else
            {
                Add(entity);
            }
        }
    }


    public class HypixelContext : DbContext
    {
        public DbSet<SaveAuction> Auctions { get; set; }

        public DbSet<SaveBids> Bids { get; set; }

        public DbSet<Player> Players { get; set; }

        public DbSet<ProductInfo> BazaarPrices { get; set; }
        public DbSet<BazaarPull> BazaarPull { get; set; }
        public DbSet<SubscribeItem> SubscribeItem { get; set; }
        public DbSet<DBItem> Items { get; set; }
        public DbSet<AlternativeName> AltItemNames { get; set; }

        public DbSet<AveragePrice> Prices { get; set; }
        public DbSet<Enchantment> Enchantment { get; set; }

        public DbSet<GoogleUser> Users { get; set; }
        public DbSet<NBTLookup> NBTLookups { get; set; }
        public DbSet<NBTKey> NBTKeys { get; set; }
        public DbSet<NBTValue> NBTValues { get; set; }
        public DbSet<Bonus> Boni { get; set; }

        public static string DbContextId = SimplerConfig.SConfig.Instance["DBConnection"];
        public static string DBVersion = SimplerConfig.SConfig.Instance["DBVersion"] ?? "10.3";

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(DbContextId,new MariaDbServerVersion(DBVersion),
            opts => opts.CommandTimeout(60).MaxBatchSize(100)).EnableSensitiveDataLogging();
        }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


            modelBuilder.Entity<SaveAuction>(entity =>
            {
                entity.HasIndex(e => e.End);
                entity.HasIndex(e => e.SellerId);
                entity.HasIndex(e => new { e.ItemId, e.End });
                entity.HasMany(e=>e.NBTLookup).WithOne().HasForeignKey("AuctionId");
                entity.HasIndex(e => e.UId).IsUnique();
                //entity.HasOne<NbtData>(d=>d.NbtData);
                //entity.HasMany<Enchantment>(e=>e.Enchantments);
            });

            modelBuilder.Entity<SaveBids>(entity =>
            {
                entity.HasIndex(e => e.BidderId);
            });

            modelBuilder.Entity<NbtData>(entity =>
            {
                entity.HasKey(e => e.Id);
            });


            modelBuilder.Entity<ProductInfo>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProductId);
            });

            modelBuilder.Entity<BazaarPull>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Timestamp);
            });


            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasKey(e => e.UuId);
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Id);
                entity.HasIndex(e => e.UId);
                //entity.Property(e=>e.Id).ValueGeneratedOnAdd();
                //entity.HasMany(p=>p.Auctions).WithOne().HasForeignKey(a=>a.SellerId).HasPrincipalKey(p=>p.Id);
                //entity.HasMany(p=>p.Bids).WithOne().HasForeignKey(a=>a.BidderId).HasPrincipalKey(p=>p.Id);

            });


            modelBuilder.Entity<DBItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Tag).IsUnique();
            });

            modelBuilder.Entity<AlternativeName>(entity =>
            {
                entity.HasIndex(e => e.Name);
            });


            modelBuilder.Entity<AveragePrice>(entity =>
            {
                entity.HasIndex(e => new { e.ItemId, e.Date }).IsUnique();
            });

            modelBuilder.Entity<Enchantment>(entity =>
            {
                entity.HasIndex(e => new { e.ItemType, e.Type, e.Level });
            });

            modelBuilder.Entity<GoogleUser>(entity =>
            {
                entity.HasIndex(e => e.GoogleId);
            });

            modelBuilder.Entity<NBTLookup>(entity =>
            {
                entity.HasKey(e => new {e.AuctionId,e.KeyId});
                entity.HasIndex(e => new {e.KeyId,e.Value});
            });

            modelBuilder.Entity<NBTKey>(entity =>
            {
                entity.HasIndex(e => e.Slug);
            });

            modelBuilder.Entity<Bonus>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
            });
        }
    }
}