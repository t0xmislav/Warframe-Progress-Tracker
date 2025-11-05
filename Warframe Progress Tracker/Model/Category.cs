using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warframe_Progress_Tracker.Model
{
    public class Category
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }

        public override bool Equals(object? obj)
        {
            return obj is Category category &&
                   Id == category.Id &&
                   DisplayName == category.DisplayName;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, DisplayName);
        }
    }
}
