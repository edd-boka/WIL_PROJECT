using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Progress_DomainLayer.Models
{
    public class Mark: BaseEntity
    {
        public int Id { get; set; }
        public string Subject { get; set; }
        public string Test1 { get; set; }
        public string Test2 { get; set; }
        public string Test3 { get; set; }
        public string Test4 { get; set; }
        public string Test5 { get; set; }
        public string Test6 { get; set; }
        public string Test7 { get; set; }
        public string Test8 { get; set; }
        public string Average { get; set; }
        public bool Checked { get; set; }
    }
}
