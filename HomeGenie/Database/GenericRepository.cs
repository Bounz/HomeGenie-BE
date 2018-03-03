using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HomeGenie.Service;
using HomeGenie.Service.Constants;
using LiteDB;

namespace HomeGenie.Database
{
    public class GenericRepository<T> where T : class
    {
        private const int LongRequestThresholdMs = 1000;

        private readonly string _dbFileName;
        private readonly string _collectionName;

        public GenericRepository(string dbFileName)
        {
            var attribute = typeof(T).GetCustomAttribute<LiteDbCollectionAttribute>();
            _dbFileName = dbFileName;
            _collectionName = attribute.CollectionName;
        }
        
        protected void Execute(Action<LiteDatabase> action, [CallerMemberName] string caller = "")
        {
            var sw = StartExecute();
            using (var db = new LiteDatabase(_dbFileName))
            {
                action(db);
            }
            StopExecute(sw, caller);
        }

        protected void Execute(Action<LiteCollection<T>> action, [CallerMemberName] string caller = "")
        {
            var sw = StartExecute();
            using (var db = new LiteDatabase(_dbFileName))
            {
                var collection = db.GetCollection<T>(_collectionName);
                action(collection);
            }
            StopExecute(sw, caller);
        }

        protected TOut Execute<TOut>(Func<LiteCollection<T>, TOut> action, [CallerMemberName] string caller = "")
        {
            TOut result;
            var sw = StartExecute();
            using (var db = new LiteDatabase(_dbFileName))
            {
                var collection = db.GetCollection<T>(_collectionName);
                result = action(collection);
            }
            StopExecute(sw, caller);
            return result;
        }

        private static Stopwatch StartExecute()
        {
            var sw = new Stopwatch();
            sw.Start();
            return sw;
        }

        private static void StopExecute(Stopwatch sw, string caller)
        {
            sw.Stop();
            if (sw.ElapsedMilliseconds <= LongRequestThresholdMs)
                return;

            //Console.WriteLine($"[Long request] Method = {caller}, Elapsed = {sw.ElapsedMilliseconds} ms.");
            HomeGenieService.LogDebug(
                Domains.HomeAutomation_HomeGenie,
                "LongRequestLogger",
                $"[Long request] Method = {caller}, Elapsed = {sw.ElapsedMilliseconds} ms.",
                null, null
            );
        }
    }
}
