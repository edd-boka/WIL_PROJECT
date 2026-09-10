using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Progress_DomainLayer.Models
{
    public class BaseEntity
    {
        public int Id { get; set; } = 1;
        public string Subject { get; set; }
        public string Test1 { get; set; }
        public string Test2 { get; set; }
        public string Average { get; set; }
        public bool Checked { get; set; }
    }
}
