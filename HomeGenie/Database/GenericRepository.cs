using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HomeGenie.Service;
using HomeGenie.Service.Constants;
using LiteDB;
using Logger = NLog.Logger;

namespace HomeGenie.Database
{
    public class GenericRepository<T> where T : class
    {
        private const int LongRequestThresholdMs = 1000;

        private readonly string _connectionString;
        private readonly string _collectionName;

        private readonly Logger _log;

        protected GenericRepository(string connectionString, Logger log)
        {
            _log = log;
            var attribute = typeof(T).GetCustomAttribute<LiteDbCollectionAttribute>();
            _connectionString = connectionString;
            _collectionName = attribute.CollectionName;
        }
        
        protected void Execute(Action<LiteDatabase> action, [CallerMemberName] string caller = "")
        {
            var sw = StartExecute();
            using (var db = new LiteDatabase(_connectionString))
            {
                action(db);
            }
            StopExecute(sw, caller);
        }

        protected void Execute(Action<ILiteCollection<T>> action, [CallerMemberName] string caller = "")
        {
            void PerformAction(LiteDatabase db)
            {
                var collection = db.GetCollection<T>(_collectionName);
                action(collection);
            }

            var sw = StartExecute();
            using (var db = new LiteDatabase(_connectionString))
            {
                try
                {
                    PerformAction(db);
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    db.Rebuild();
                    PerformAction(db);
                }

            }
            StopExecute(sw, caller);
        }

        protected TOut Execute<TOut>(Func<ILiteCollection<T>, TOut> action, [CallerMemberName] string caller = "")
        {
            TOut PerformAction(LiteDatabase db)
            {
                var collection = db.GetCollection<T>(_collectionName);
                return action(collection);
            }

            TOut result;
            var sw = StartExecute();
            using (var db = new LiteDatabase(_connectionString))
            {
                try
                {
                    result = PerformAction(db);
                }
                catch (Exception e)
                {
                    _log.Error(e);
                    db.Rebuild();
                    result = PerformAction(db);
                }
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
