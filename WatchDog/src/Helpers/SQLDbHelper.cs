﻿using System.Data;
using System.Threading.Tasks;
using Dapper;
using WatchDog.src.Data;
using WatchDog.src.Models;
using WatchDog.src.Utilities;

namespace WatchDog.src.Helpers {
    internal static class SQLDbHelper {
        // WATCHLOG OPERATIONS
        public static async Task<Page<WatchLog>> GetAllWatchLogs(string searchString, string verbString, string statusCode, int pageNumber) {
            var query = @$"SELECT * FROM {PersistenceHelper.GetRequestsTable()} ";

            if (!string.IsNullOrEmpty(searchString) || !string.IsNullOrEmpty(verbString) || !string.IsNullOrEmpty(statusCode))
                query += "WHERE ";

            if (!string.IsNullOrEmpty(searchString)) {
                if (GeneralHelper.IsPostgres())
                    query += $"({nameof(WatchLog.Path)} LIKE '%{searchString}%' OR {nameof(WatchLog.Method)} LIKE '%{searchString}%' OR {nameof(WatchLog.ResponseStatus)}::text LIKE '%{searchString}%' OR {nameof(WatchLog.QueryString)} LIKE '%{searchString}%')" + (string.IsNullOrEmpty(statusCode) && string.IsNullOrEmpty(verbString) ? "" : " AND ");
                else
                    query += $"({nameof(WatchLog.Path)} LIKE '%{searchString}%' OR {nameof(WatchLog.Method)} LIKE '%{searchString}%' OR {nameof(WatchLog.ResponseStatus)} LIKE '%{searchString}%' OR {nameof(WatchLog.QueryString)} LIKE '%{searchString}%')" + (string.IsNullOrEmpty(statusCode) && string.IsNullOrEmpty(verbString) ? "" : " AND ");
            }

            if (!string.IsNullOrEmpty(verbString)) {
                query += $"{nameof(WatchLog.Method)} LIKE '%{verbString}%' " + (string.IsNullOrEmpty(statusCode) ? "" : "AND ");
            }

            if (!string.IsNullOrEmpty(statusCode)) {
                query += $"{nameof(WatchLog.ResponseStatus)} = {statusCode}";
            }
            query += $" ORDER BY {nameof(WatchLog.Id)} DESC";
            using(var connection = ExternalDbContext.CreateSQLConnection()) {
                connection.Open();
                var logs = await connection.QueryAsync<WatchLog>(query);
                connection.Close();
                return logs.ToPaginatedList(pageNumber, CustomConfiguration.PageSize);
            }
        }

        public static async Task InsertWatchLog(WatchLog log) {
            bool isPostgres = GeneralHelper.IsPostgres();
            var query = @$"INSERT INTO {PersistenceHelper.GetRequestsTable()} (responseBody,responseStatus,requestBody,queryString,path,requestHeaders,responseHeaders,method,host,ipAddress,timeSpent,startTime,endTime,userName) " +
                "VALUES (@ResponseBody,@ResponseStatus,@RequestBody,@QueryString,@Path,@RequestHeaders,@ResponseHeaders,@Method,@Host,@IpAddress,@TimeSpent,@StartTime,@EndTime, @UserName);";

            var parameters = new DynamicParameters();
            parameters.Add("ResponseBody", isPostgres ? log.ResponseBody.Replace("\u0000", "") : log.ResponseBody, DbType.String);
            parameters.Add("ResponseStatus", log.ResponseStatus, DbType.Int32);
            parameters.Add("RequestBody", isPostgres ? log.RequestBody.Replace("\u0000", "") : log.RequestBody, DbType.String);
            parameters.Add("QueryString", log.QueryString, DbType.String);
            parameters.Add("Path", log.Path, DbType.String);
            parameters.Add("RequestHeaders", log.RequestHeaders, DbType.String);
            parameters.Add("ResponseHeaders", log.ResponseHeaders, DbType.String);
            parameters.Add("Method", log.Method, DbType.String);
            parameters.Add("Host", log.Host, DbType.String);
            parameters.Add("IpAddress", log.IpAddress, DbType.String);
            parameters.Add("TimeSpent", log.TimeSpent, DbType.String);
            parameters.Add("UserName", log.UserName, DbType.String);

            if (isPostgres) {
                parameters.Add("StartTime", log.StartTime.ToUniversalTime(), DbType.DateTime);
                parameters.Add("EndTime", log.EndTime.ToUniversalTime(), DbType.DateTime);
            } else {
                parameters.Add("StartTime", log.StartTime);
                parameters.Add("EndTime", log.EndTime);
            }

            using(var connection = ExternalDbContext.CreateSQLConnection()) {
                connection.Open();
                await connection.ExecuteAsync(query, parameters);
                connection.Close();
            }
        }

        // WATCH EXCEPTION OPERATIONS
        public static async Task<Page<WatchExceptionLog>> GetAllWatchExceptionLogs(string searchString, int pageNumber) {
            var query = @$"SELECT * FROM {PersistenceHelper.GetExceptionsTable()} ";
            if (!string.IsNullOrEmpty(searchString)) {
                searchString = searchString.ToLower();
                query += $"WHERE {nameof(WatchExceptionLog.Source)} LIKE '%{searchString}%' OR {nameof(WatchExceptionLog.Message)} LIKE '%{searchString}%' OR {nameof(WatchExceptionLog.StackTrace)} LIKE '%{searchString}%' ";
            }
            query += $"ORDER BY {nameof(WatchExceptionLog.Id)} DESC";
            using(var connection = ExternalDbContext.CreateSQLConnection()) {
                var logs = await connection.QueryAsync<WatchExceptionLog>(query);
                return logs.ToPaginatedList(pageNumber, CustomConfiguration.PageSize);
            }
        }

        public static async Task InsertWatchExceptionLog(WatchExceptionLog log) {
            var query = @$"INSERT INTO {PersistenceHelper.GetExceptionsTable()} (message,stackTrace,typeOf,source,path,method,queryString,requestBody,encounteredAt,userName) " +
                "VALUES (@Message,@StackTrace,@TypeOf,@Source,@Path,@Method,@QueryString,@RequestBody,@EncounteredAt,@UserName);";

            var parameters = new DynamicParameters();
            parameters.Add("Message", log.Message, DbType.String);
            parameters.Add("StackTrace", log.StackTrace, DbType.String);
            parameters.Add("TypeOf", log.TypeOf, DbType.String);
            parameters.Add("Source", log.Source, DbType.String);
            parameters.Add("Path", log.Path, DbType.String);
            parameters.Add("Method", log.Method, DbType.String);
            parameters.Add("QueryString", log.QueryString, DbType.String);
            parameters.Add("RequestBody", log.RequestBody, DbType.String);
            parameters.Add("UserName", log.UserName, DbType.String);

            if (GeneralHelper.IsPostgres()) {
                parameters.Add("EncounteredAt", log.EncounteredAt.ToUniversalTime(), DbType.DateTime);
            } else {
                parameters.Add("EncounteredAt", log.EncounteredAt, DbType.DateTime);
            }

            using(var connection = ExternalDbContext.CreateSQLConnection()) {
                await connection.ExecuteAsync(query, parameters);
            }
        }

        // LOGS OPERATION
        public static async Task<Page<WatchLoggerModel>> GetAllLogs(string searchString, string logLevelString, int pageNumber) {
            var query = @$"SELECT * FROM {PersistenceHelper.GetLogsTable()} ";

            if (!string.IsNullOrEmpty(searchString) || !string.IsNullOrEmpty(logLevelString))
                query += "WHERE ";

            if (!string.IsNullOrEmpty(searchString)) {
                searchString = searchString.ToLower();
                query += $"{nameof(WatchLoggerModel.CallingFrom)} LIKE '%{searchString}%' OR {nameof(WatchLoggerModel.CallingMethod)} LIKE '%{searchString}%' OR {nameof(WatchLoggerModel.Message)} LIKE '%{searchString}%' OR {nameof(WatchLoggerModel.EventId)} LIKE '%{searchString}%' " + (string.IsNullOrEmpty(logLevelString) ? "" : "AND ");
            }

            if (!string.IsNullOrEmpty(logLevelString)) {
                query += $"{nameof(WatchLoggerModel.LogLevel)} LIKE '%{logLevelString}%' ";
            }
            query += $"ORDER BY {nameof(WatchLoggerModel.Id)} DESC";

            using(var connection = ExternalDbContext.CreateSQLConnection()) {
                connection.Open();
                var logs = await connection.QueryAsync<WatchLoggerModel>(query);
                connection.Close();
                return logs.ToPaginatedList(pageNumber, CustomConfiguration.PageSize);
            }
        }

        public static async Task InsertLog(WatchLoggerModel log) {
            var query = @$"INSERT INTO {PersistenceHelper.GetLogsTable()} (message,eventId,timestamp,callingFrom,callingMethod,lineNumber,logLevel) " +
                "VALUES (@Message,@EventId,@Timestamp,@CallingFrom,@CallingMethod,@LineNumber,@LogLevel);";

            var parameters = new DynamicParameters();
            parameters.Add("Message", log.Message, DbType.String);
            parameters.Add("CallingFrom", log.CallingFrom, DbType.String);
            parameters.Add("CallingMethod", log.CallingMethod, DbType.String);
            parameters.Add("LineNumber", log.LineNumber, DbType.Int32);
            parameters.Add("LogLevel", log.LogLevel, DbType.String);
            parameters.Add("EventId", log.EventId, DbType.String);

            if (GeneralHelper.IsPostgres()) {
                parameters.Add("Timestamp", log.Timestamp.ToUniversalTime(), DbType.DateTime);
            } else {
                parameters.Add("Timestamp", log.Timestamp, DbType.DateTime);
            }

            using(var connection = ExternalDbContext.CreateSQLConnection()) {
                await connection.ExecuteAsync(query, parameters);
            }
        }

        public static async Task<bool> ClearLogs() {
            var watchlogQuery = @$"truncate table {PersistenceHelper.GetRequestsTable()}";
            var exQuery = @$"truncate table {PersistenceHelper.GetExceptionsTable()}";
            var logQuery = @$"truncate table {PersistenceHelper.GetLogsTable()}";
            using(var connection = ExternalDbContext.CreateSQLConnection()) {
                var watchlogs = await connection.ExecuteAsync(watchlogQuery);
                var exLogs = await connection.ExecuteAsync(exQuery);
                var logs = await connection.ExecuteAsync(logQuery);
                return watchlogs > 1 && exLogs > 1 && logs > 1;
            }
        }
    }
}
