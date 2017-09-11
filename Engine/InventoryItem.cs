using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine
{
    public class InventoryItem
    {
        public Item Details { get; set; }
        public int Quanitity { get; set; }

        public InventoryItem(Item details, int quantity)
        {
            Details = details;
            Quanitity = quantity;
        }
    }
}
