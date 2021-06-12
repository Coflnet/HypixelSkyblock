using System;
using System.Collections.Generic;
using System.Linq;

namespace hypixel.Filter
{
    public class FilterEngine
    {
        private FilterDictonary Filters = new FilterDictonary();

        public FilterEngine()
        {
            
            Filters.Add<ReforgeFilter>();
            Filters.Add<RarityFilter>();
            Filters.Add<PetLevelFilter>();
            Filters.Add<PetItemFilter>();
            Filters.Add<EnchantmentFilter>();
            Filters.Add<EnchantLvlFilter>();
            Filters.Add<UIdFilter>();
            Filters.Add<EndBeforeFilter>();
            Filters.Add<EndAfterFilter>();
        }

        public IQueryable<SaveAuction> AddFilters(IQueryable<SaveAuction> query, Dictionary<string, string> filters)
        {
            var args = new FilterArgs(filters);
            foreach (var filter in filters)
            {
                if (!Filters.TryGetValue(filter.Key, out IFilter filterObject))
                    throw new CoflnetException("filter_unknown", $"The filter {filter.Key} is not know, please remove it");
                query = filterObject.AddQuery(query, args);
            }

            return query;
        }

        public IEnumerable<SaveAuction> Filter(IEnumerable<SaveAuction> items, Dictionary<string, string> filters)
        {
            var args = new FilterArgs(filters);
            foreach (var filter in filters)
            {
                if (!Filters.TryGetValue(filter.Key, out IFilter filterObject))
                    throw new CoflnetException("filter_unknown", $"The filter {filter.Key} is not know, please remove it");
                items = filterObject.Filter(items, args);
            }

            return items;
        }

        public IEnumerable<IFilter> FiltersFor(DBItem item)
        {
            try 
            {
                return Filters.Values.Where(f=>{
                    try 
                    {
                        return f.IsApplicable(item);
                    } catch(Exception e)
                    {
                        Console.WriteLine($"Could not get filter {f.Name} for Item {item.Id} {item.Tag}. \n{e.StackTrace}");
                        return false;
                    }});
            } catch(Exception e)
            {
                Console.WriteLine($"Could not get filter for Item {item.Id} {item.Tag}. \n{e.StackTrace}");
                return new IFilter[0];
            }
        }

        public IFilter GetFilter(string name)
        {
            if(!Filters.TryGetValue(name, out IFilter value))
                throw new CoflnetException("unknown_filter",$"There is no filter with name {name}");
            return value;
        }
    }
}
