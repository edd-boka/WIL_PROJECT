using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Progress_DomainLayer.Models;
using Progress_RepositoryLayer.IRepository;
using Progress_ServiceLayer.ICustomServices;

namespace Progress_ServiceLayer.CustomServices
{
    public class MarkService : ICustomService<Mark>
    {
        private readonly IRepository<Mark> _markRepository;

        public MarkService(IRepository<Mark> markRepository)
        {
            _markRepository = markRepository;
        }
        public void Create(Mark entity)
        {
            try
            {
                if (entity != null)
                {
                    _markRepository.Create(entity);
                    _markRepository.SaveChanges();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Delete(Mark entity)
        {
            try
            {
                if (entity != null)
                {
                    _markRepository.Delete(entity);
                    _markRepository.SaveChanges();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }



        public void Edit(Mark entity)
        {
            try
            {
                if (entity != null)
                {
                    _markRepository.Edit(entity);
                    _markRepository.SaveChanges();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public Mark Get(int Id)
        {
            try
            {
                var obj = _markRepository.Get(Id);
                if (obj != null)
                {
                    return obj;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public IEnumerable<Mark> GetAll()
        {
            try
            {
                var obj = _markRepository.GetAll();
                if (obj != null)
                {
                    return obj;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
