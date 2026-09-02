using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Progress_DomainLayer.Models;

namespace Progress_RepositoryLayer.IRepository
{
    public interface IRepository<T> where T: BaseEntity
    {
        IEnumerable<T> GetAll();
        T Get(int id);
        void Create(T entity);
        void Edit(T entity);
        void Delete(T entity);
        void SaveChanges();
    }
}
