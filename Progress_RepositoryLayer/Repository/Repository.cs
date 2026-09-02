using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Progress_DomainLayer.Data;
using Progress_DomainLayer.Models;
using Progress_RepositoryLayer.IRepository;

namespace Progress_RepositoryLayer.Repository
{
    public class Repository<T> : IRepository<T> where T : BaseEntity
    {
        private readonly SQLiteDBContext _dbContext;
        private DbSet<T> _dbSet;

        public Repository(SQLiteDBContext dbContext)
        {
            _dbContext = dbContext;
            _dbSet = _dbContext.Set<T>();
        }
        public void Create(T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("Entity is null on Create: Repository");
            }
            _dbSet.Add(entity);
            _dbContext.SaveChanges();
        }

        public void Delete(T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("Entity is null on Delete: Repository");
            }
            _dbSet.Remove(entity);
            _dbContext.SaveChanges();
        }

        public void Edit(T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("Entity is null on Edit: Repository");
            }
            _dbSet.Update(entity);
            _dbContext.SaveChanges();
        }

        public T Get(int Id)
        {
            return _dbSet.SingleOrDefault(c => c.Id == Id);
        }

        public IEnumerable<T> GetAll()
        {

            return _dbSet.AsEnumerable();
        }

        public void SaveChanges()
        {
            _dbContext.SaveChanges();
        }
    }
}
