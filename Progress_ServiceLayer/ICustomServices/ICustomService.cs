using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Progress_ServiceLayer.ICustomServices
{
    public interface ICustomService<T> where T: class
    {
        IEnumerable<T> GetAll();
        T Get(int Id);
        void Create(T entity);
        void Edit(T entity);
        void Delete(T entity);
    }
}
