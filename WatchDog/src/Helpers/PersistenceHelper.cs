using System;
using System.Linq;

using WatchDog.src.Models;

namespace WatchDog.src.Utilities {
    internal static class PersistenceHelper {
        public const string WatchLogTableName = "wd_requests";
        public const string WatchLogExceptionTableName = "wd_exceptions";
        public const string WatchDogMongoCounterTableName = "wd_counter";
        public const string LogsTableName = "wd_logs";

        public static string GetRequestsTable() => toTableName(CustomConfiguration.TableNamePrefix, WatchLogTableName);
        public static string GetExceptionsTable() => toTableName(CustomConfiguration.TableNamePrefix, WatchLogExceptionTableName);
        public static string GetMongoCounterTable() => toTableName(CustomConfiguration.TableNamePrefix, WatchDogMongoCounterTableName);
        public static string GetLogsTable() => toTableName(CustomConfiguration.TableNamePrefix, LogsTableName);
        private static string toTableName(string tableNamePrefix, string watchLogTableName) => string.Join("_", new[] {tableNamePrefix, watchLogTableName}.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}
