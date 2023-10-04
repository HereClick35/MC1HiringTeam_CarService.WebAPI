using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CarService.WebAPI.Data;
using CarService.WebAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CarService.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CarsController : ControllerBase
    {
        private readonly ICarsService _carsService;
        private readonly IMemoryCache _cache;
        private readonly int _timeslidingExpiration = 10000;
        private readonly int _timeabsoluteExpiration = 30000;
        public CarsController(ICarsService carsService, IMemoryCache memoryCache)
        {
            _carsService = carsService;
            _cache = memoryCache;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            Car user;
            
            #region --|Criando CacheKey|--
            var cacheKey = string.Format("GetBy{0}", id);
            #endregion

            #region --|Validando Cache e Realizando busca no DB|--
            if (!_cache.TryGetValue<Car>(cacheKey, out user))
            {
                user = (await _carsService.Get(new[] { id }, null)).FirstOrDefault();
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromSeconds(_timeslidingExpiration))
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(_timeabsoluteExpiration));

                _cache.Set(cacheKey, user, cacheOptions);
            }
            #endregion

            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [HttpGet("")]
        public async Task<IActionResult> GetAll([FromQuery] Filters filters)
        {
            IEnumerable<Car> cars;

            #region --|Criando CacheKey|--
            var _years = "";
            var _makes = "";
            var _models = "";
            if (filters.Years != null) for (int i = 0; i < filters.Years.Length; i++) { _years += filters.Years[i]; }
            if (filters.Makes != null) for (int i = 0; i < filters.Makes.Length; i++) { _makes += filters.Makes[i]; }
            if (filters.Models != null) for (int i = 0; i < filters.Models.Length; i++) { _models += filters.Models[i]; }
            var cacheKey = string.Format("GetByAll{0}{1}{2}", _years, _makes, _models);
            #endregion

            #region --|Validando Cache e Realizando busca no DB|--
            if (!_cache.TryGetValue<IEnumerable<Car>>(cacheKey, out cars))
            {
                cars = await _carsService.Get(null, filters);
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromSeconds(_timeslidingExpiration))
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(_timeabsoluteExpiration));

                _cache.Set(cacheKey, cars, cacheOptions);
            }
            #endregion
            return Ok(cars);
        }

        [HttpPost]
        public async Task<IActionResult> Add(Car car)
        {
            await _carsService.Add(car);
            // Limpando Cache
            ClearCache();
            return Ok(car);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = (await _carsService.Get(new[] { id }, null)).FirstOrDefault();
            if (user == null)
                return NotFound();

            await _carsService.Delete(user);
            //Limpando Cache
            ClearCache();
            return NoContent();
        }
        private void ClearCache()
        {
            // Realizando limpeza do Cache
            var field = typeof(MemoryCache).GetProperty("EntriesCollection", BindingFlags.NonPublic | BindingFlags.Instance);
            var collection = field.GetValue(_cache) as ICollection;
            if (collection != null)
                foreach (var item in collection)
                {
                    var methodInfo = item.GetType().GetProperty("Key");
                    var val = methodInfo.GetValue(item);
                    _cache.Remove(val.ToString());
                }
        }
    }
}